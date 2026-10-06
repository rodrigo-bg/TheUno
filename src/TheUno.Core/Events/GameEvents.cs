using TheUno.Core.Models;

namespace TheUno.Core.Events;

public record CardPlayedEventArgs(Player Player, Card Card, CardColor EffectiveColor);

public record CardDrawnEventArgs(Player Player, Card? DrawnCard);

public record TurnChangedEventArgs(Player ActivePlayer, int Direction, Card TopCard, CardColor ActiveColor);

public record GameOverEventArgs(Player Winner, TimeSpan Duration, int TotalTurns);