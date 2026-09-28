namespace TheUno.Core.Contracts;

public enum GameActionType
{
    DrawCard,
    PlayCard,
    PassTurn,
    CallUno,
    UnoPenalty
}

public record GameLogEntryDto(
    Guid MatchId,
    int TurnNumber,
    Guid PlayerId,
    GameActionType Action,
    string CardDetails,
    string? ChosenColor,
    DateTime Timestamp
);

public interface IGameLogRepository
{
    Task LogActionAsync(GameLogEntryDto entry);
    Task<List<GameLogEntryDto>> GetLogsByMatchAsync(Guid matchId);
}