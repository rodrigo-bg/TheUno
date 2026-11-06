using System;
using System.Threading.Tasks;
using TheUno.Core.Contracts;
using TheUno.Core.Engine;
using TheUno.Core.Events;

namespace TheUno.Infrastructure.Services;

public class GameAuditService
{
    private readonly IGameLogRepository _logRepository;
    private readonly IMatchRepository _matchRepository;
    private IGameEngine? _engine;
    private Guid _matchId;

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

        _engine.OnCardPlayed += HandleCardPlayed;
        _engine.OnCardDrawn += HandleCardDrawn;
        _engine.OnGameOver += HandleGameOver;
    }

    public void Detach()
    {
        if (_engine != null)
        {
            _engine.OnCardPlayed -= HandleCardPlayed;
            _engine.OnCardDrawn -= HandleCardDrawn;
            _engine.OnGameOver -= HandleGameOver;
            _engine = null;
        }
    }

    private async void HandleCardPlayed(CardPlayedEventArgs e)
    {
        try
        {
            var playerId = Guid.TryParse(e.Player.Name, out var id) ? id : Guid.Empty;
            var entry = new GameLogEntryDto(
                _matchId,
                0,
                playerId,
                GameActionType.PlayCard,
                $"{e.Card.Color}_{e.Card.Type}",
                e.ActiveColor.ToString(),
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
            var playerId = Guid.TryParse(e.Player.Name, out var id) ? id : Guid.Empty;
            var entry = new GameLogEntryDto(
                _matchId,
                0,
                playerId,
                GameActionType.DrawCard,
                $"{e.Card.Color}_{e.Card.Type}",
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

    private async void HandleGameOver(GameOverEventArgs e)
    {
        try
        {
            var winnerId = Guid.TryParse(e.Winner.Name, out var id) ? id : Guid.Empty;
            await _matchRepository.CompleteMatchAsync(_matchId, winnerId, e.TotalTurns, e.Duration);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameAuditService Warning] Error finalizando partida en auditoría: {ex.Message}");
        }
    }
}
