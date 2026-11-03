using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TheUno.Core.Contracts;
using TheUno.Core.Engine;
using TheUno.Core.Events;
using TheUno.Core.Models;
using TheUno.UI.Services;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Controla qué pantalla se muestra en la ventana principal (menú, tablero o historial)
/// y la música que suena en cada una. También registra cada partida en la API.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // Los 3 integrantes del equipo con los mismos Ids que la semilla de la base de datos,
    // en el orden de los turnos. Los nombres son los mismos que se eligen en la sala.
    private static readonly (Guid Id, string Name)[] SeedPlayers =
    {
        (Guid.Parse("22222222-2222-2222-2222-222222222222"), "TONATIUH"),
        (Guid.Parse("11111111-1111-1111-1111-111111111111"), "RODRIGO"),
        (Guid.Parse("33333333-3333-3333-3333-333333333333"), "ALEJANDRO")
    };

    private readonly MusicPlayer _music;
    private readonly IMatchRepository _matchRepository;
    private readonly IGameLogRepository _logRepository;

    // Crea la conexión con la sala; sin ella la sala solo espera (todavía no hay servidor)
    private readonly Func<ILobbyService>? _createLobby;

    [ObservableProperty]
    private ViewModelBase _currentView;

    /// <summary>
    /// Se está creando la partida en la API: se muestra "CARGANDO..." y se ignoran más clics,
    /// para no iniciar dos partidas seguidas.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    public MainWindowViewModel(MusicPlayer music, IMatchRepository matchRepository, IGameLogRepository logRepository,
        Func<ILobbyService>? createLobby = null)
    {
        _music = music;
        _matchRepository = matchRepository;
        _logRepository = logRepository;
        _createLobby = createLobby;
        _currentView = CreateMenu();
        PlayMusicFor(_currentView);
    }

    partial void OnCurrentViewChanged(ViewModelBase value) => PlayMusicFor(value);

    // La partida tiene música tranquila; el menú y las demás pantallas, la movida.
    private void PlayMusicFor(ViewModelBase view) =>
        _music.Play(view is BoardViewModel ? MusicPlayer.BoardTrack : MusicPlayer.MenuTrack);

    private MenuViewModel CreateMenu() => new(() => ShowLobby(isHost: true), () => ShowLobby(isHost: false), ShowHistory);

    /// <summary>
    /// Crear o unirse a partida: cada vez es una sala nueva. Al volver a jugar se regresa aquí
    /// con el mismo papel (quien creó la partida la vuelve a crear; quien se unió se reconecta a la misma IP).
    /// </summary>
    private void ShowLobby(bool isHost, string? reconnectIp = null)
    {
        LobbyViewModel? lobby = null;
        lobby = new LobbyViewModel(_createLobby?.Invoke(), isHost, ShowMenu,
            name => StartGame(name, () => ShowLobby(isHost, lobby!.ServerIp)), reconnectIp);
        CurrentView = lobby;
    }

    /// <summary>
    /// Empieza una partida nueva. Primero se crea en la API; si responde, el motor guarda cada movimiento
    /// con ese Id. Si la API no está abierta, se juega igual pero sin guardar.
    /// El jugador elegido en la sala queda abajo (Jugador 1); los otros siguen en el orden de los turnos.
    /// "Volver a jugar" en la pantalla final llama a playAgain, que regresa a la sala.
    /// </summary>
    private async void StartGame(string myPlayerName, Action playAgain)
    {
        if (IsLoading) return;

        var mySeat = Array.FindIndex(SeedPlayers, p => p.Name == myPlayerName);
        var players = SeedPlayers
            .Select((_, i) => SeedPlayers[(Math.Max(mySeat, 0) + i) % SeedPlayers.Length])
            .Select(p => new Player(p.Id, p.Name))
            .ToList();

        IsLoading = true;
        var matchId = await TryCreateMatchAsync(players);
        IsLoading = false;

        var engine = new GameEngine(matchId is null ? null : _logRepository);
        engine.StartGame(players, matchId ?? Guid.NewGuid());

        Action<Player, int>? onUnoCalled = null;
        if (matchId is { } savedMatchId)
        {
            engine.OnGameOver += e => _ = TryCompleteMatchAsync(savedMatchId, e);
            onUnoCalled = (player, turn) => _ = TryLogUnoAsync(savedMatchId, player, turn);
        }

        // Cada partida usa un motor nuevo; el tablero lo recibe ya iniciado.
        CurrentView = new BoardViewModel(engine, ShowMenu, playAgain, onUnoCalled);
    }

    /// <summary>
    /// Guarda en el log que un jugador cantó UNO, con el mismo formato que usa el motor.
    /// (El motor solo lo registra con su CallUno(), que la interfaz no usa por la ventana de 2 segundos.)
    /// </summary>
    private async Task TryLogUnoAsync(Guid matchId, Player player, int turnNumber)
    {
        try
        {
            await _logRepository.LogActionAsync(new GameLogEntryDto(
                matchId, turnNumber, player.Id, GameActionType.CallUno, "Declaro UNO", null, DateTime.UtcNow));
        }
        catch (Exception)
        {
            // Si la API se cerró durante la partida, este movimiento no se guarda
        }
    }

    private async Task<Guid?> TryCreateMatchAsync(IEnumerable<Player> players)
    {
        try
        {
            var matchId = await _matchRepository.CreateMatchAsync(players.Select(p => p.Id));
            return matchId == Guid.Empty ? null : matchId;
        }
        catch (Exception)
        {
            // API cerrada o sin respuesta: la partida no se guarda
            return null;
        }
    }

    private async Task TryCompleteMatchAsync(Guid matchId, GameOverEventArgs e)
    {
        try
        {
            await _matchRepository.CompleteMatchAsync(matchId, e.Winner.Id, e.TotalTurns, e.Duration);
        }
        catch (Exception)
        {
            // Si la API se cerró durante la partida, el resultado no se guarda
        }
    }

    private void ShowHistory()
    {
        CurrentView = new StatsViewModel(ShowMenu, _matchRepository, _logRepository);
    }

    // Al regresar se crea un menú nuevo; la partida o el historial anterior se descartan.
    private void ShowMenu()
    {
        CurrentView = CreateMenu();
    }
}
