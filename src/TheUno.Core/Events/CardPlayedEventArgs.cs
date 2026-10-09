using System;
using TheUno.Core.Models;

namespace TheUno.Core.Events;

public class CardPlayedEventArgs : EventArgs
{
    public Player Player { get; }
    public Card Card { get; }
    public CardColor ActiveColor { get; }

    public CardPlayedEventArgs(Player player, Card card, CardColor activeColor)
    {
        Player = player;
        Card = card;
        ActiveColor = activeColor;
    }
}