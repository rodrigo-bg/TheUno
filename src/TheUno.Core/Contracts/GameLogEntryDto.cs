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
        Guid MatchId,
        int TurnNumber,
        Guid PlayerId,
        GameActionType Action,
        string CardDetails,
        string? ChosenColor,
        DateTime Timestamp)
    {
        this.MatchId = MatchId;
        this.TurnNumber = TurnNumber;
        this.PlayerId = PlayerId;
        this.Action = Action;
        this.CardDetails = CardDetails;
        this.ChosenColor = ChosenColor;
        this.Timestamp = Timestamp;
    }
}