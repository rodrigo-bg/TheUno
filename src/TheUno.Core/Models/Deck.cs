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
        Reset();
    }

    public int GetDrawPileCount() // Numero de cartas en la pila boca abajo
    {
        return _drawPile.Count;
    }

    public int GetDiscardPileCount() // Numero de cartas en la pila boca arriba
    {
        return _discardPile.Count;
    }

    public Card? GetTopCard()
    {
        if (_discardPile.Count == 0)
        {
            return null;
        }
        return _discardPile[_discardPile.Count - 1];
    }

    public void Reset() // Limpia por completo la baraja y vuelve a crear las 108 cartas
    {
        _drawPile.Clear();
        _discardPile.Clear();

        CardColor[] standardColors = new CardColor[]
        {
            CardColor.Red,
            CardColor.Blue,
            CardColor.Green,
            CardColor.Yellow
        };

        for (int c = 0; c < standardColors.Length; c++)
        {
            CardColor color = standardColors[c];

            // 1 carta '0' por color
            _drawPile.Add(new Card(Guid.NewGuid(), color, CardType.Number, 0));

            // 2 cartas del '1' al '9' por color
            for (int number = 1; number <= 9; number++)
            {
                _drawPile.Add(new Card(Guid.NewGuid(), color, CardType.Number, number));
                _drawPile.Add(new Card(Guid.NewGuid(), color, CardType.Number, number));
            }

            // 2 cartas de accion por color
            for (int i = 0; i < 2; i++)
            {
                _drawPile.Add(new Card(Guid.NewGuid(), color, CardType.Skip, null));
                _drawPile.Add(new Card(Guid.NewGuid(), color, CardType.Reverse, null));
                _drawPile.Add(new Card(Guid.NewGuid(), color, CardType.DrawTwo, null));
            }
        }

        // 4 comodines Wild y 4 Wild Draw Four
        for (int i = 0; i < 4; i++)
        {
            _drawPile.Add(new Card(Guid.NewGuid(), CardColor.None, CardType.Wild, null));
            _drawPile.Add(new Card(Guid.NewGuid(), CardColor.None, CardType.WildDrawFour, null));
        }

        Shuffle();
    }

    public void Shuffle() // Algoritmo estandar de mezcla de barajas
    {
        for (int i = _drawPile.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);

            // Intercambio tradicional usando variable temporal
            Card temp = _drawPile[i];
            _drawPile[i] = _drawPile[j];
            _drawPile[j] = temp;
        }
    }

    public Card? Draw() 
    {
        if (_drawPile.Count == 0)
        {
            RecycleDiscardPile();
        }

        if (_drawPile.Count == 0)
        {
            return null;
        }

        int lastIndex = _drawPile.Count - 1;
        Card drawnCard = _drawPile[lastIndex];
        _drawPile.RemoveAt(lastIndex);
        return drawnCard;
    }

    public void Discard(Card card)
    {
        _discardPile.Add(card);
    }

    public Card SetupFirstDiscardCard()
    {
        Card? card = Draw();
        if (card == null)
        {
            throw new InvalidOperationException("No hay cartas suficientes para iniciar la partida.");
        }

        while (card.Type == CardType.WildDrawFour)
        {
            _drawPile.Insert(0, card);
            Shuffle();
            card = Draw();
            if (card == null)
            {
                throw new InvalidOperationException("No hay cartas suficientes para iniciar la partida.");
            }
        }

        Discard(card);
        return card;
    }

    private void RecycleDiscardPile()
    {
        if (_discardPile.Count <= 1)
        {
            return;
        }

        int topIndex = _discardPile.Count - 1;
        Card activeTopCard = _discardPile[topIndex];
        _discardPile.RemoveAt(topIndex);

        for (int i = 0; i < _discardPile.Count; i++)
        {
            _drawPile.Add(_discardPile[i]);
        }

        _discardPile.Clear();
        _discardPile.Add(activeTopCard);

        Shuffle();
    }
}