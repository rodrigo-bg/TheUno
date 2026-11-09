using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TheUno.Core.Contracts;
using TheUno.Core.Engine;
using TheUno.Core.Events;
using TheUno.Core.Models;
using TheUno.Server.Contracts;

namespace TheUno.Server.Services;

/// <summary>
/// Servicio singleton que mantiene vivo el motor de juego (GameEngine) en la memoria del servidor,
/// gestiona los lugares de la sala de espera, asocia las conexiones de SignalR con cada jugador
/// y construye el estado filtrado individualmente (ocultando las manos rivales).
/// </summary>
public class GameSessionManager
{
    public const int UnoWindowMilliseconds = 2000;

    // Semilla oficial de jugadores sincronizada con los Ids y nombres de la base de datos SQLite y la UI
    private static readonly (Guid Id, string Name)[] SeedPlayers =
    {
        (Guid.Parse("11111111-1111-1111-1111-111111111111"), "Rodri"),
        (Guid.Parse("22222222-2222-2222-2222-222222222222"), "Tona"),
        (Guid.Parse("33333333-3333-3333-3333-333333333333"), "Alejandro")
    };

    private readonly object _syncLock = new object();
    private readonly IMatchRepository _matchRepository;
    private readonly IGameLogRepository _logRepository;

    // Mapa de ConnectionId -> Nombre del jugador ("Rodri", "Tona", "Alejandro")
    private readonly Dictionary<string, string> _connectionToPlayer = new Dictionary<string, string>();
    private readonly List<string> _joinedPlayers = new List<string>();
    private string? _hostConnectionId;

    // Estado de la partida activa en el servidor
    private GameEngine? _engine;
    private Guid? _persistedMatchId;
    private bool _isGameOver;
    private bool _canPassCurrentTurn;
    private int _turnNumber;

    // Control de la ventana de 2 segundos para cantar UNO en el servidor
    private Player? _unoWindowPlayer;
    private int _unoWindowTurnNumber;
    private CancellationTokenSource? _unoTimerCts;

    // Captura de eventos disparados síncronamente por el motor durante una jugada
    private string? _pendingPenaltyPlayerName;
    private GameOverEventArgs? _pendingGameOverArgs;

    /// <summary>
    /// Evento disparado cuando el temporizador de 2 segundos de UNO expira en el servidor
    /// y es necesario difundir el nuevo turno a todos los clientes conectados.
    /// </summary>
    public event Func<Task>? OnUnoWindowExpired;

    public GameSessionManager(IMatchRepository matchRepository, IGameLogRepository logRepository)
    {
        _matchRepository = matchRepository ?? throw new ArgumentNullException(nameof(matchRepository));
        _logRepository = logRepository ?? throw new ArgumentNullException(nameof(logRepository));
    }

    // ==========================================
    // GESTIÓN DE LA SALA DE ESPERA (LOBBY)
    // ==========================================

    public bool TryCreateGame(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return false;
        }

        lock (_syncLock)
        {
            // Si hay una partida en curso, no se permite crear otra
            if (_engine != null && !_isGameOver)
            {
                return false;
            }

            // Si otra computadora ya creó la sala de espera, se rechaza crear una segunda partida
            if (_hostConnectionId != null && _hostConnectionId != connectionId)
            {
                return false;
            }

            // Si había una partida previa ya terminada, se limpia el estado para abrir nueva sala
            if (_engine != null)
            {
                ResetRoomInternal();
            }

            _hostConnectionId = connectionId;
            return true;
        }
    }

    public void ClearFinishedGame()
    {
        lock (_syncLock)
        {
            if (_isGameOver)
            {
                ResetRoomInternal();
            }
        }
    }

    public List<string> GetTakenPlayers()
    {
        lock (_syncLock)
        {
            return new List<string>(_joinedPlayers);
        }
    }

    public bool ChoosePlayer(string connectionId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(playerName))
        {
            return false;
        }

        string? canonicalName = ResolveCanonicalPlayerName(playerName);
        if (canonicalName == null)
        {
            return false;
        }

        lock (_syncLock)
        {
            // Si hay una partida activa en curso, no se permite cambiar ni elegir jugador
            if (_engine != null && !_isGameOver)
            {
                return false;
            }

            // Si otro cliente ya eligió ese jugador, se rechaza
            if (_joinedPlayers.Contains(canonicalName))
            {
                if (_connectionToPlayer.TryGetValue(connectionId, out string? existing) && existing == canonicalName)
                {
                    return true;
                }
                return false;
            }

            // Si esta misma conexión tenía otro nombre antes, lo libera
            if (_connectionToPlayer.TryGetValue(connectionId, out string? previousName))
            {
                _joinedPlayers.Remove(previousName);
            }

            _connectionToPlayer[connectionId] = canonicalName;
            _joinedPlayers.Add(canonicalName);
            return true;
        }
    }

    public async Task<bool> StartGameAsync()
    {
        List<Player> playersToStart;

        lock (_syncLock)
        {
            if (_joinedPlayers.Count != SeedPlayers.Length)
            {
                return false;
            }

            // Determinar el asiento inicial (el Host empieza como Jugador 1 de la rotación)
            string firstPlayerName = _joinedPlayers[0];
            if (_hostConnectionId != null && _connectionToPlayer.TryGetValue(_hostConnectionId, out string? hostName))
            {
                firstPlayerName = hostName;
            }

            int startSeat = 0;
            for (int i = 0; i < SeedPlayers.Length; i++)
            {
                if (SeedPlayers[i].Name == firstPlayerName)
                {
                    startSeat = i;
                    break;
                }
            }

            playersToStart = new List<Player>(SeedPlayers.Length);
            for (int i = 0; i < SeedPlayers.Length; i++)
            {
                var seed = SeedPlayers[(startSeat + i) % SeedPlayers.Length];
                playersToStart.Add(new Player(seed.Id, seed.Name));
            }
        }

        // Registrar la partida en la API de persistencia (si está disponible)
        Guid? matchId = await TryCreateMatchInApiAsync(playersToStart);

        lock (_syncLock)
        {
            CancelUnoTimerInternal();

            _persistedMatchId = matchId;
            _isGameOver = false;
            _canPassCurrentTurn = false;
            _unoWindowPlayer = null;
            _pendingPenaltyPlayerName = null;
            _pendingGameOverArgs = null;

            _engine = new GameEngine(matchId.HasValue ? _logRepository : null);
            _engine.OnTurnChanged += HandleEngineTurnChanged;
            _engine.OnUnoPenalty += HandleEngineUnoPenalty;
            _engine.OnGameOver += HandleEngineGameOver;

            _engine.StartGame(playersToStart, matchId ?? Guid.NewGuid());
            _turnNumber = _engine.TopCard.Type is CardType.Skip or CardType.DrawTwo ? 2 : 1;
            return true;
        }
    }

    // ==========================================
    // ACCIONES DE JUEGO (PARTIDA EN CURSO)
    // ==========================================

    public bool TryPlayCard(
        string connectionId,
        Guid cardId,
        CardColor? chosenColor,
        out string? unoWindowPlayerName,
        out string? penaltyPlayerName,
        out (string WinnerName, double DurationSeconds)? gameOverInfo)
    {
        unoWindowPlayerName = null;
        penaltyPlayerName = null;
        gameOverInfo = null;

        lock (_syncLock)
        {
            if (!TryGetActiveTurnPlayer(connectionId, out GameEngine engine, out Player actingPlayer))
            {
                return false;
            }

            Card? cardToPlay = null;
            for (int i = 0; i < actingPlayer.Hand.Count; i++)
            {
                if (actingPlayer.Hand[i].Id == cardId)
                {
                    cardToPlay = actingPlayer.Hand[i];
                    break;
                }
            }

            if (cardToPlay == null || !engine.CanPlayCard(cardToPlay))
            {
                return false;
            }

            _pendingPenaltyPlayerName = null;
            _pendingGameOverArgs = null;
            int turnPlayed = _turnNumber;

            engine.PlayCard(cardToPlay, chosenColor);
            _canPassCurrentTurn = false;

            penaltyPlayerName = _pendingPenaltyPlayerName;
            _pendingPenaltyPlayerName = null;

            if (_pendingGameOverArgs != null)
            {
                GameOverEventArgs over = _pendingGameOverArgs;
                _pendingGameOverArgs = null;
                gameOverInfo = (over.Winner.Name, over.Duration.TotalSeconds);
                return true;
            }

            // Si el jugador se quedó con 1 sola carta y no había cantado UNO, se abre la ventana de 2 segundos
            if (actingPlayer.Hand.Count == 1 && !actingPlayer.HasDeclaredUno)
            {
                _unoWindowPlayer = actingPlayer;
                _unoWindowTurnNumber = turnPlayed;
                unoWindowPlayerName = actingPlayer.Name;
                StartUnoWindowTimerInternal();
            }

            return true;
        }
    }

    public bool TryDrawCard(string connectionId, out string? penaltyPlayerName)
    {
        penaltyPlayerName = null;

        lock (_syncLock)
        {
            if (!TryGetActiveTurnPlayer(connectionId, out GameEngine engine, out Player actingPlayer))
            {
                return false;
            }

            if (_canPassCurrentTurn)
            {
                return false;
            }

            _pendingPenaltyPlayerName = null;

            engine.DrawCard();

            // Si después de robar sigue siendo el turno del mismo jugador, significa que la carta robada es jugable
            _canPassCurrentTurn = engine.CurrentPlayer.Id == actingPlayer.Id;

            penaltyPlayerName = _pendingPenaltyPlayerName;
            _pendingPenaltyPlayerName = null;
            return true;
        }
    }

    public bool TryPassTurn(string connectionId)
    {
        lock (_syncLock)
        {
            if (!TryGetActiveTurnPlayer(connectionId, out GameEngine engine, out _))
            {
                return false;
            }

            if (!_canPassCurrentTurn)
            {
                return false;
            }

            engine.PassTurn();
            _canPassCurrentTurn = false;
            return true;
        }
    }

    public bool TryCallUno(string connectionId)
    {
        Player? playerToLog = null;
        int turnToLog = 0;
        Guid? matchIdToLog = null;

        lock (_syncLock)
        {
            if (_engine == null || _isGameOver)
            {
                return false;
            }

            if (!_connectionToPlayer.TryGetValue(connectionId, out string? playerName))
            {
                return false;
            }

            // Caso 1: El jugador canta UNO dentro de su ventana de 2 segundos tras tirar su penúltima carta
            if (_unoWindowPlayer != null && _unoWindowPlayer.Name == playerName)
            {
                _unoWindowPlayer.HasDeclaredUno = true;
                playerToLog = _unoWindowPlayer;
                turnToLog = _unoWindowTurnNumber;
                matchIdToLog = _persistedMatchId;

                CancelUnoTimerInternal();
                _unoWindowPlayer = null;
            }
            // Caso 2: El jugador en turno canta UNO antes de tirar su penúltima carta
            else if (_unoWindowPlayer == null && _engine.CurrentPlayer.Name == playerName && _engine.CurrentPlayer.Hand.Count <= 2)
            {
                _engine.CallUno();
            }
            else
            {
                return false;
            }
        }

        if (playerToLog != null && matchIdToLog.HasValue)
        {
            _ = TryLogUnoInApiAsync(matchIdToLog.Value, playerToLog, turnToLog);
        }

        return true;
    }

    // ==========================================
    // SALIDA Y DESCONEXIÓN DE JUGADORES
    // ==========================================

    public void RemoveConnection(string connectionId, out bool wasGameInterrupted, out string? leftPlayerName)
    {
        wasGameInterrupted = false;
        leftPlayerName = null;

        lock (_syncLock)
        {
            if (!_connectionToPlayer.TryGetValue(connectionId, out string? playerName))
            {
                if (_hostConnectionId == connectionId)
                {
                    _hostConnectionId = null;
                }
                return;
            }

            leftPlayerName = playerName;
            _connectionToPlayer.Remove(connectionId);
            _joinedPlayers.Remove(playerName);

            if (_hostConnectionId == connectionId)
            {
                _hostConnectionId = null;
            }

            // Si la partida estaba activa y aún no terminaba, se interrumpe para todos y se limpia la sala
            if (_engine != null && !_isGameOver)
            {
                wasGameInterrupted = true;
                ResetRoomInternal();
            }
            else if (_engine != null && _isGameOver && _joinedPlayers.Count == 0)
            {
                ResetRoomInternal();
            }
        }
    }

    // ==========================================
    // CONSTRUCCIÓN DE ESTADO INDIVIDUAL POR JUGADOR
    // ==========================================

    /// <summary>
    /// Devuelve la lista de pares (ConnectionId, GameStateDto) personalizados para cada cliente.
    /// Garantiza que cada cliente reciba únicamente sus propias cartas en MyHand,
    /// y que de los rivales solo viaje el conteo de cartas (CardCount).
    /// </summary>
    public List<(string ConnectionId, GameStateDto State)> CreatePersonalizedStates()
    {
        lock (_syncLock)
        {
            List<(string ConnectionId, GameStateDto State)> result = new List<(string ConnectionId, GameStateDto State)>();

            if (_engine == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, string> entry in _connectionToPlayer)
            {
                string connectionId = entry.Key;
                string playerName = entry.Value;

                GameStateDto? state = BuildStateForPlayerInternal(_engine, playerName);
                if (state != null)
                {
                    result.Add((connectionId, state));
                }
            }

            return result;
        }
    }

    private GameStateDto? BuildStateForPlayerInternal(GameEngine engine, string playerName)
    {
        Player? targetPlayer = null;
        for (int i = 0; i < engine.Players.Count; i++)
        {
            if (engine.Players[i].Name == playerName)
            {
                targetPlayer = engine.Players[i];
                break;
            }
        }

        if (targetPlayer == null)
        {
            return null;
        }

        // 1. Mano privada exclusiva del jugador dueño de esta conexión
        List<CardDto> myHand = new List<CardDto>(targetPlayer.Hand.Count);
        for (int i = 0; i < targetPlayer.Hand.Count; i++)
        {
            myHand.Add(new CardDto(targetPlayer.Hand[i]));
        }

        // 2. Estado público de los 3 jugadores (NUNCA incluye las cartas, solo cuántas tienen)
        List<PlayerStateDto> publicPlayers = new List<PlayerStateDto>(engine.Players.Count);
        for (int i = 0; i < engine.Players.Count; i++)
        {
            Player p = engine.Players[i];
            bool hasUnoVisible = p.HasDeclaredUno && p.Hand.Count == 1;
            publicPlayers.Add(new PlayerStateDto(p.Name, p.Hand.Count, hasUnoVisible));
        }

        // 3. Determinar el jugador en turno visible y las acciones permitidas
        string effectiveCurrentPlayerName;
        List<Guid> playableCardIds = new List<Guid>();
        bool canDraw = false;
        bool canPass = false;

        if (_unoWindowPlayer != null)
        {
            // Mientras corre la ventana de 2 segundos de UNO, el turno permanece detenido en quien tiró
            effectiveCurrentPlayerName = _unoWindowPlayer.Name;
        }
        else
        {
            effectiveCurrentPlayerName = engine.CurrentPlayer.Name;

            if (!_isGameOver && effectiveCurrentPlayerName == playerName)
            {
                for (int i = 0; i < targetPlayer.Hand.Count; i++)
                {
                    Card card = targetPlayer.Hand[i];
                    if (engine.CanPlayCard(card))
                    {
                        playableCardIds.Add(card.Id);
                    }
                }

                canPass = _canPassCurrentTurn;
                canDraw = !_canPassCurrentTurn;
            }
        }

        CardDto topCardDto = new CardDto(engine.TopCard);

        return new GameStateDto(
            myName: playerName,
            myHand: myHand,
            playableCardIds: playableCardIds,
            players: publicPlayers,
            currentPlayerName: effectiveCurrentPlayerName,
            topCard: topCardDto,
            activeColor: engine.ActiveColor,
            direction: engine.Direction,
            canDraw: canDraw,
            canPass: canPass
        );
    }

    // ==========================================
    // MÉTODOS AUXILIARES Y EVENTOS DEL MOTOR
    // ==========================================

    private bool TryGetActiveTurnPlayer(string connectionId, out GameEngine engine, out Player actingPlayer)
    {
        engine = null!;
        actingPlayer = null!;

        if (_engine == null || _isGameOver || _unoWindowPlayer != null)
        {
            return false;
        }

        if (!_connectionToPlayer.TryGetValue(connectionId, out string? playerName))
        {
            return false;
        }

        if (_engine.CurrentPlayer.Name != playerName)
        {
            return false;
        }

        engine = _engine;
        actingPlayer = _engine.CurrentPlayer;
        return true;
    }

    private void HandleEngineTurnChanged(TurnChangedEventArgs e)
    {
        _turnNumber++;
    }

    private void HandleEngineUnoPenalty(Player penalizedPlayer)
    {
        _pendingPenaltyPlayerName = penalizedPlayer.Name;
    }

    private void HandleEngineGameOver(GameOverEventArgs e)
    {
        _isGameOver = true;
        _pendingGameOverArgs = e;

        if (_persistedMatchId.HasValue)
        {
            Guid matchId = _persistedMatchId.Value;
            _ = TryCompleteMatchInApiAsync(matchId, e);
        }
    }

    private void StartUnoWindowTimerInternal()
    {
        CancelUnoTimerInternal();

        CancellationTokenSource cts = new CancellationTokenSource();
        _unoTimerCts = cts;
        CancellationToken token = cts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(UnoWindowMilliseconds, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            bool shouldBroadcast = false;
            lock (_syncLock)
            {
                if (!token.IsCancellationRequested && _unoWindowPlayer != null)
                {
                    _unoWindowPlayer = null;
                    _unoTimerCts = null;
                    shouldBroadcast = true;
                }
            }

            if (shouldBroadcast && OnUnoWindowExpired != null)
            {
                await OnUnoWindowExpired.Invoke();
            }
        }, token);
    }

    private void CancelUnoTimerInternal()
    {
        if (_unoTimerCts != null)
        {
            _unoTimerCts.Cancel();
            _unoTimerCts.Dispose();
            _unoTimerCts = null;
        }
    }

    private void ResetRoomInternal()
    {
        CancelUnoTimerInternal();
        _engine = null;
        _persistedMatchId = null;
        _isGameOver = false;
        _canPassCurrentTurn = false;
        _unoWindowPlayer = null;
        _pendingPenaltyPlayerName = null;
        _pendingGameOverArgs = null;
        _hostConnectionId = null;
        _connectionToPlayer.Clear();
        _joinedPlayers.Clear();
    }

    private static string? ResolveCanonicalPlayerName(string playerName)
    {
        string trimmed = playerName.Trim();
        for (int i = 0; i < SeedPlayers.Length; i++)
        {
            if (string.Equals(SeedPlayers[i].Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return SeedPlayers[i].Name;
            }
        }

        if (string.Equals(trimmed, "RODRIGO", StringComparison.OrdinalIgnoreCase))
        {
            return "Rodri";
        }

        if (string.Equals(trimmed, "TONATIUH", StringComparison.OrdinalIgnoreCase))
        {
            return "Tona";
        }

        return null;
    }

    private async Task<Guid?> TryCreateMatchInApiAsync(IEnumerable<Player> players)
    {
        try
        {
            Guid matchId = await _matchRepository.CreateMatchAsync(players.Select(p => p.Id));
            return matchId == Guid.Empty ? null : matchId;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task TryCompleteMatchInApiAsync(Guid matchId, GameOverEventArgs e)
    {
        try
        {
            await _matchRepository.CompleteMatchAsync(matchId, e.Winner.Id, e.TotalTurns, e.Duration);
        }
        catch (Exception)
        {
            // Si la API se cerró durante la partida, el juego continúa sin caerse
        }
    }

    private async Task TryLogUnoInApiAsync(Guid matchId, Player player, int turnNumber)
    {
        try
        {
            GameLogEntryDto entry = new GameLogEntryDto(
                matchId,
                turnNumber,
                player.Id,
                GameActionType.CallUno,
                "Declaro UNO",
                null,
                DateTime.UtcNow
            );
            await _logRepository.LogActionAsync(entry);
        }
        catch (Exception)
        {
            // Ignorar fallos de red hacia FastAPI para no interrumpir la partida
        }
    }
}
