using System;
using System.Collections.Generic;

namespace TheUno.Core.Models;

public class Deck
{
    private readonly List<Card> _drawPile;
    private readonly List<Card> _discardPile;
    private readonly Random _random;

    public Deck()
    {
        _drawPile = new List<Card>();
        _discardPile = new List<Card>();
        _random = new Random();
    }
}