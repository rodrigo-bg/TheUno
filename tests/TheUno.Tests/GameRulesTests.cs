using System;
using System.Collections.Generic;
using TheUno.Core.Engine;
using TheUno.Core.Models;
using Xunit;

namespace TheUno.Tests;

public class GameRulesTests
{
    private readonly GameRules _rules = new();

    [Fact]
    public void IsPlayLegal_MatchingColor_ReturnsTrue()
    {
        var topCard = new Card(Guid.NewGuid(), CardColor.Red, CardType.Number, 7);
        var playedCard = new Card(Guid.NewGuid(), CardColor.Red, CardType.Number, 2);
        var hand = new List<Card> { playedCard };

        var result = _rules.IsPlayLegal(playedCard, topCard, CardColor.Red, hand);

        Assert.True(result);
    }

    [Fact]
    public void IsPlayLegal_MatchingNumberDifferentColor_ReturnsTrue()
    {
        var topCard = new Card(Guid.NewGuid(), CardColor.Red, CardType.Number, 7);
        var playedCard = new Card(Guid.NewGuid(), CardColor.Blue, CardType.Number, 7);
        var hand = new List<Card> { playedCard };

        var result = _rules.IsPlayLegal(playedCard, topCard, CardColor.Red, hand);

        Assert.True(result);
    }

    [Fact]
    public void IsPlayLegal_WildCard_AlwaysReturnsTrue()
    {
        var topCard = new Card(Guid.NewGuid(), CardColor.Yellow, CardType.Number, 4);
        var wildCard = new Card(Guid.NewGuid(), CardColor.None, CardType.Wild, null);
        var hand = new List<Card> { wildCard };

        var result = _rules.IsPlayLegal(wildCard, topCard, CardColor.Yellow, hand);

        Assert.True(result);
    }

    [Fact]
    public void IsPlayLegal_DifferentColorAndDifferentValue_ReturnsFalse()
    {
        var topCard = new Card(Guid.NewGuid(), CardColor.Green, CardType.Number, 3);
        var playedCard = new Card(Guid.NewGuid(), CardColor.Blue, CardType.Number, 8);
        var hand = new List<Card> { playedCard };

        var result = _rules.IsPlayLegal(playedCard, topCard, CardColor.Green, hand);

        Assert.False(result);
    }

    [Fact]
    public void CalculateHandScore_MustSumStandardUnoValues()
    {
        var player = new Player("Jugador");
        player.AddCard(new Card(Guid.NewGuid(), CardColor.Red, CardType.Number, 5));          // 5 pts
        player.AddCard(new Card(Guid.NewGuid(), CardColor.Blue, CardType.Skip, null));        // 20 pts
        player.AddCard(new Card(Guid.NewGuid(), CardColor.None, CardType.WildDrawFour, null)); // 50 pts

        var score = player.CalculateHandScore();

        Assert.Equal(75, score);
    }
}
