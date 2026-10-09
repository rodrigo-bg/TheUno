using System;
using TheUno.Core.Models;

namespace TheUno.Core.Events;

public class CardDrawnEventArgs : EventArgs
{
    public Player Player { get; }
    public Card Card { get; }

    public CardDrawnEventArgs(Player player, Card card)
    {
        Player = player;
        Card = card;
    }
}