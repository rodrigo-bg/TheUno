using System;
using TheUno.Core.Models;

namespace TheUno.Core.Events;

public class CardDrawnEventArgs : EventArgs
{
    public Player Player { get; }
    public Card Card { get; }

    // Compatibilidad con TheUno.Infrastructure
    public Card DrawnCard
    {
        get
        {
            return Card;
        }
    }

    public CardDrawnEventArgs(Player player, Card card)
    {
        Player = player;
        Card = card;
    }
}