using System;
using System.Threading.Tasks;
using TheUno.Core.Contracts;
using TheUno.Core.Engine;
using TheUno.Core.Events;
using TheUno.Core.Models;

namespace TheUno.Infrastructure.Services;

public class GameAuditService
{
    private readonly IGameLogRepository _logRepository;
    private readonly IMatchRepository _matchRepository;
    private IGameEngine? _engine;
    private Guid _matchId;

    // Turno actual para la bitácora: empieza en 1 y sube con cada cambio de turno del motor
    private int _turnNumber;

    public GameAuditService(IGameLogRepository logRepository, IMatchRepository matchRepository)
    {
        _logRepository = logRepository ?? throw new ArgumentNullException(nameof(logRepository));
        _matchRepository = matchRepository ?? throw new ArgumentNullException(nameof(matchRepository));
    }

    /// <summary>
    /// Vincula el servicio de auditoría a las instancias del motor de juego y la partida actual,
    /// garantizando el registro automático de eventos con resiliencia ante desconexiones.
    /// </summary>
    public void Attach(IGameEngine engine, Guid matchId)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _matchId = matchId;
        _turnNumber = 1;

        _engine.OnCardPlayed += HandleCardPlayed;
        _engine.OnCardDrawn += HandleCardDrawn;
        _engine.OnTurnChanged += HandleTurnChanged;
        _engine.OnGameOver += HandleGameOver;
    }

    public void Detach()
    {
        if (_engine != null)
        {
            _engine.OnCardPlayed -= HandleCardPlayed;
            _engine.OnCardDrawn -= HandleCardDrawn;
            _engine.OnTurnChanged -= HandleTurnChanged;
            _engine.OnGameOver -= HandleGameOver;
            _engine = null;
        }
    }

    private async void HandleCardPlayed(CardPlayedEventArgs e)
    {
        try
        {
            // Mismo formato que el log del motor ("Red 7", "Wild"); el color elegido solo va en comodines
            var isWild = e.Card.Type is CardType.Wild or CardType.WildDrawFour;
            var entry = new GameLogEntryDto(
                _matchId,
                _turnNumber,
                e.Player.Id,
                GameActionType.PlayCard,
                e.Card.GetDisplayText(),
                isWild ? e.ActiveColor.ToString() : null,
                DateTime.UtcNow
            );
            await _logRepository.LogActionAsync(entry);
        }
        catch (Exception ex)
        {
            // Resiliencia ante desconexión de la API REST / Base de datos central
            System.Diagnostics.Debug.WriteLine($"[GameAuditService Warning] Error registrando jugada: {ex.Message}");
        }
    }

    private async void HandleCardDrawn(CardDrawnEventArgs e)
    {
        try
        {
            var entry = new GameLogEntryDto(
                _matchId,
                _turnNumber,
                e.Player.Id,
                GameActionType.DrawCard,
                e.Card.GetDisplayText(),
                null,
                DateTime.UtcNow
            );
            await _logRepository.LogActionAsync(entry);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameAuditService Warning] Error registrando robo de carta: {ex.Message}");
        }
    }

    private void HandleTurnChanged(TurnChangedEventArgs e) => _turnNumber++;

    private async void HandleGameOver(GameOverEventArgs e)
    {
        try
        {
            await _matchRepository.CompleteMatchAsync(_matchId, e.Winner.Id, e.TotalTurns, e.Duration);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameAuditService Warning] Error finalizando partida en auditoría: {ex.Message}");
        }
    }
}
