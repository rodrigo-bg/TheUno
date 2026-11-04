using System;
using TheUno.Core.Models;

namespace TheUno.Server.Contracts;

/// <summary>
/// Representa una carta serializable para su transmisión por red hacia el cliente dueño de la mano
/// o para mostrar la carta superior de la pila de descarte.
/// </summary>
public class CardDto
{
    public Guid Id { get; }
    public CardColor Color { get; }
    public CardType Type { get; }
    public int? Number { get; }

    public CardDto(Guid id, CardColor color, CardType type, int? number)
    {
        Id = id;
        Color = color;
        Type = type;
        Number = number;
    }

    public CardDto(Card card)
    {
        if (card == null)
        {
            throw new ArgumentNullException(nameof(card));
        }

        Id = card.Id;
        Color = card.Color;
        Type = card.Type;
        Number = card.Number;
    }
}
