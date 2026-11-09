using System;

namespace TheUno.Server.Contracts;

/// <summary>
/// Estado público de un jugador en la mesa. Por seguridad y reglas del juego,
/// jamás incluye las cartas de su mano, únicamente el conteo total y si declaró UNO.
/// </summary>
public class PlayerStateDto
{
    public string Name { get; }
    public int CardCount { get; }
    public bool HasCalledUno { get; }

    public PlayerStateDto(string name, int cardCount, bool hasCalledUno)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del jugador no puede estar vacío.", nameof(name));
        }

        Name = name;
        CardCount = cardCount;
        HasCalledUno = hasCalledUno;
    }
}
