using System;
using System.Collections.Generic;
using System.Linq;
using TheUno.Core.Models;
using Xunit;

namespace TheUno.Tests;

public class DeckTests
{
    [Fact]
    public void Constructor_OrReset_MustCreateExactly108CardsInDrawPile()
    {
        var deck = new Deck();

        Assert.Equal(108, deck.GetDrawPileCount());
        Assert.Equal(0, deck.GetDiscardPileCount());
        Assert.Null(deck.GetTopCard());
    }

    [Fact]
    public void Draw_AllCards_MustContainOfficialUnoCardDistribution()
    {
        var deck = new Deck();
        var drawnCards = new List<Card>();

        // Vaciar el mazo extrayendo las 108 cartas
        for (int i = 0; i < 108; i++)
        {
            var card = deck.Draw();
            Assert.NotNull(card);
            drawnCards.Add(card!);
        }

        Assert.Equal(0, deck.GetDrawPileCount());
        Assert.Equal(108, drawnCards.Count);

        // 4 comodines de cambio de color y 4 comodines +4
        Assert.Equal(4, drawnCards.Count(c => c.Type == CardType.Wild));
        Assert.Equal(4, drawnCards.Count(c => c.Type == CardType.WildDrawFour));

        // 8 de cada carta especial (2 por color x 4 colores)
        Assert.Equal(8, drawnCards.Count(c => c.Type == CardType.Skip));
        Assert.Equal(8, drawnCards.Count(c => c.Type == CardType.Reverse));
        Assert.Equal(8, drawnCards.Count(c => c.Type == CardType.DrawTwo));

        // 4 cartas '0' (1 por cada color)
        Assert.Equal(4, drawnCards.Count(c => c.Type == CardType.Number && c.Number == 0));

        // 72 cartas del '1' al '9' (2 por cada valor x 9 valores x 4 colores)
        Assert.Equal(72, drawnCards.Count(c => c.Type == CardType.Number && c.Number >= 1 && c.Number <= 9));
    }

    [Fact]
    public void SetupFirstDiscardCard_MustNeverBeWildDrawFourAndMustPlaceCardInDiscardPile()
    {
        var deck = new Deck();

        var firstCard = deck.SetupFirstDiscardCard();

        Assert.NotNull(firstCard);
        Assert.NotEqual(CardType.WildDrawFour, firstCard.Type);
        Assert.Equal(107, deck.GetDrawPileCount());
        Assert.Equal(1, deck.GetDiscardPileCount());
        Assert.Equal(firstCard, deck.GetTopCard());
    }

    [Fact]
    public void Draw_WhenDrawPileIsEmpty_MustRecycleDiscardPileExceptTopCard()
    {
        var deck = new Deck();

        // Vaciar completamente la pila de robo inicial
        for (int i = 0; i < 108; i++)
        {
            deck.Draw();
        }
        Assert.Equal(0, deck.GetDrawPileCount());

        // Colocar 3 cartas en la pila de descarte
        var card1 = new Card(Guid.NewGuid(), CardColor.Red, CardType.Number, 1);
        var card2 = new Card(Guid.NewGuid(), CardColor.Blue, CardType.Number, 2);
        var card3 = new Card(Guid.NewGuid(), CardColor.Green, CardType.Number, 3); // Ultima en llegar = carta activa

        deck.Discard(card1);
        deck.Discard(card2);
        deck.Discard(card3);

        Assert.Equal(3, deck.GetDiscardPileCount());
        Assert.Equal(card3, deck.GetTopCard());

        // Al robar con la pila vacia, se reciclan card1 y card2, manteniendo card3 en el descarte
        var drawnCard = deck.Draw();

        Assert.NotNull(drawnCard);
        // La pila de descarte debe retener solo la carta superior activa
        Assert.Equal(1, deck.GetDiscardPileCount());
        Assert.Equal(card3, deck.GetTopCard());
        // De las 2 cartas recicladas al mazo, robamos 1, por lo que queda 1
        Assert.Equal(1, deck.GetDrawPileCount());
    }
}