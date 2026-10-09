using System;
using TheUno.Core.Models;

namespace TheUno.Core.Events;

public class TurnChangedEventArgs : EventArgs
{
    public Player CurrentPlayer { get; }
    public int Direction { get; }
    public Card TopCard { get; }
    public CardColor ActiveColor { get; }

    public TurnChangedEventArgs(Player currentPlayer, int direction, Card topCard, CardColor activeColor)
    {
        CurrentPlayer = currentPlayer;
        Direction = direction;
        TopCard = topCard;
        ActiveColor = activeColor;
    }
}