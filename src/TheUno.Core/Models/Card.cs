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
    
    public int GetScoreValue()
    {
        switch (Type)
        {
            case CardType.Number:
                if (Number.HasValue)
                {
                    return Number.Value;
                }
                return 0;

            case CardType.Skip:
            case CardType.Reverse:
            case CardType.DrawTwo:
                return 20;

            case CardType.Wild:
            case CardType.WildDrawFour:
                return 50;

            default:
                return 0;
        }
    }
    
    public string GetDisplayText()
    {
        switch (Type)
        {
            case CardType.Number:
                return Color.ToString() + " " + Number.ToString();

            case CardType.Wild:
                return "Wild";

            case CardType.WildDrawFour:
                return "Wild Draw 4 (+4)";

            default:
                return Color.ToString() + " " + Type.ToString();
        }
    }
} 