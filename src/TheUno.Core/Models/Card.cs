using System;

namespace TheUno.Core.Models;

public class Card
{
    public Guid Id { get; }
    public CardColor Color { get; }
    public CardType Type { get; }
    public int? Number { get; }

    public Card(Guid id, CardColor color, CardType type, int? number = null)
    {
        Id = id;
        Color = color;
        Type = type;
        Number = number;
    }
} 