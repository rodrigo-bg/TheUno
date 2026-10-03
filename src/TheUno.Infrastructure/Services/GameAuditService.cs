using TheUno.Core.Contracts;
using TheUno.Core.Events;
using TheUno.Core.Models;

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
        _gameEngine.OnTurnChanged += HandleTurnChanged;
        _gameEngine.OnUnoPenalty += HandleUnoPenalty;
    }

    private async void HandleCardPlayed(CardPlayedEventArgs args)
    {
        var entry = new GameLogEntryDto(
            MatchId: _currentMatchId,
            TurnNumber: _currentTurnNumber++,
            PlayerId: args.Player.Id,
            Action: GameActionType.PlayCard,
            CardDetails: args.Card.ToString() ?? string.Empty,
            ChosenColor: args.EffectiveColor.ToString(),
            Timestamp: DateTime.UtcNow
        );

        await _logRepository.LogActionAsync(entry);
    }

    private async void HandleCardDrawn(CardDrawnEventArgs args)
    {
        var entry = new GameLogEntryDto(
            MatchId: _currentMatchId,
            TurnNumber: _currentTurnNumber,
            PlayerId: args.Player.Id,
            Action: GameActionType.DrawCard,
            CardDetails: args.DrawnCard?.ToString() ?? "Carta escogida",
            ChosenColor: null,
            Timestamp: DateTime.UtcNow
        );

        await _logRepository.LogActionAsync(entry);
    }

    private void HandleTurnChanged(TurnChangedEventArgs args)
    {
        // Al cambiar de turno se aumenta el contador para la siguiente acción
        _currentTurnNumber++;
    }

    private async void HandleUnoPenalty(Player player)
    {
        var entry = new GameLogEntryDto(
            MatchId: _currentMatchId,
            TurnNumber: _currentTurnNumber,
            PlayerId: player.Id,
            Action: GameActionType.UnoPenalty,
            CardDetails: "+2 carta de penal (No canto UNO)",
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
        _gameEngine.OnTurnChanged -= HandleTurnChanged;
        _gameEngine.OnUnoPenalty -= HandleUnoPenalty;
    }
}