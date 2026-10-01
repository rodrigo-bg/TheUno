using TheUno.Core.Contracts;
using TheUno.Core.Engine;

namespace TheUno.Infrastructure.Services;

public class GameAuditService : IDisposable
{
    private readonly IGameLogRepository _logRepository;
    private readonly IGameEngine _gameEngine;
    private readonly Guid _currentMatchId;
    private int _currentTurnNumber;

    public GameAuditService(IGameLogRepository logRepository, IGameEngine gameEngine, Guid matchId)
    {
        _logRepository = logRepository;
        _gameEngine = gameEngine;
        _currentMatchId = matchId;
        _currentTurnNumber = 1;

        // Suscribirse a los eventos del motor de juego
        SubscribeToEngineEvents();
    }

    private void SubscribeToEngineEvents()
    {
        _gameEngine.OnCardPlayed += HandleCardPlayed;
        _gameEngine.OnCardDrawn += HandleCardDrawn;
        _gameEngine.OnTurnPassed += HandleTurnPassed;
        _gameEngine.OnUnoCalled += HandleUnoCalled;
        _gameEngine.OnUnoPenaltyApplied += HandleUnoPenaltyApplied;
    }

    private async void HandleCardPlayed(Guid playerId, string cardDetails, string? chosenColor)
    {
        var entry = new GameLogEntryDto(
            MatchId: _currentMatchId,
            TurnNumber: _currentTurnNumber++,
            PlayerId: playerId,
            Action: GameActionType.PlayCard,
            CardDetails: cardDetails,
            ChosenColor: chosenColor,
            Timestamp: DateTime.UtcNow
        );

        await _logRepository.LogActionAsync(entry);
    }

    private async void HandleCardDrawn(Guid playerId, string cardDetails)
    {
        var entry = new GameLogEntryDto(
            MatchId: _currentMatchId,
            TurnNumber: _currentTurnNumber++,
            PlayerId: playerId,
            Action: GameActionType.DrawCard,
            CardDetails: cardDetails,
            ChosenColor: null,
            Timestamp: DateTime.UtcNow
        );

        await _logRepository.LogActionAsync(entry);
    }

    private async void HandleTurnPassed(Guid playerId)
    {
        var entry = new GameLogEntryDto(
            MatchId: _currentMatchId,
            TurnNumber: _currentTurnNumber++,
            PlayerId: playerId,
            Action: GameActionType.PassTurn,
            CardDetails: string.Empty,
            ChosenColor: null,
            Timestamp: DateTime.UtcNow
        );

        await _logRepository.LogActionAsync(entry);
    }

    private async void HandleUnoCalled(Guid playerId)
    {
        var entry = new GameLogEntryDto(
            MatchId: _currentMatchId,
            TurnNumber: _currentTurnNumber,
            PlayerId: playerId,
            Action: GameActionType.CallUno,
            CardDetails: string.Empty,
            ChosenColor: null,
            Timestamp: DateTime.UtcNow
        );

        await _logRepository.LogActionAsync(entry);
    }

    private async void HandleUnoPenaltyApplied(Guid playerId)
    {
        var entry = new GameLogEntryDto(
            MatchId: _currentMatchId,
            TurnNumber: _currentTurnNumber,
            PlayerId: playerId,
            Action: GameActionType.UnoPenalty,
            CardDetails: "+2 cards penalty",
            ChosenColor: null,
            Timestamp: DateTime.UtcNow
        );

        await _logRepository.LogActionAsync(entry);
    }

    public void Dispose()
    {
        // Cancelar suscripciones a eventos cuando la partida termine para liberar recursos
        _gameEngine.OnCardPlayed -= HandleCardPlayed;
        _gameEngine.OnCardDrawn -= HandleCardDrawn;
        _gameEngine.OnTurnPassed -= HandleTurnPassed;
        _gameEngine.OnUnoCalled -= HandleUnoCalled;
        _gameEngine.OnUnoPenaltyApplied -= HandleUnoPenaltyApplied;
    }
}