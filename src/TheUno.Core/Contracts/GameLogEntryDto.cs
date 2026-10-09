using System;

namespace TheUno.Core.Contracts;

public class GameLogEntryDto
{
    public Guid MatchId { get; }
    public int TurnNumber { get; }
    public Guid PlayerId { get; }
    public GameActionType Action { get; }
    public string CardDetails { get; }
    public string? ChosenColor { get; }
    public DateTime Timestamp { get; }

    public GameLogEntryDto(
        Guid matchId,
        int turnNumber,
        Guid playerId,
        GameActionType action,
        string cardDetails,
        string? chosenColor,
        DateTime timestamp)
    {
        MatchId = matchId;
        TurnNumber = turnNumber;
        PlayerId = playerId;
        Action = action;
        CardDetails = cardDetails;
        ChosenColor = chosenColor;
        Timestamp = timestamp;
    }
}