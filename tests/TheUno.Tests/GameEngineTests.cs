using System;
using System.Linq;
using TheUno.Core.Engine;
using TheUno.Core.Models;
using TheUno.Tests.Fakes;
using Xunit;

namespace TheUno.Tests;

public class GameEngineTests
{
    private readonly FakeGameLogRepository _logRepo;
    private readonly GameEngine _engine;

    public GameEngineTests()
    {
        _logRepo = new FakeGameLogRepository();
        _engine = new GameEngine(_logRepo);
    }

    private void StartStandardGame(string[] playerNames)
    {
        // Asegura que la partida inicie con una carta numerica para que el sentido
        // inicial sea horario (1) y el primer turno sea del jugador 1 sin saltos ni penalizaciones iniciales.
        do
        {
            _engine.StartGame(playerNames);
        } while (_engine.TopCard.Type != CardType.Number);
    }

    [Fact]
    public void StartGame_WithThreePlayers_DealsSevenCardsEachAndSetsActiveCard()
    {
        var playerNames = new[] { "Jugador 1", "Jugador 2", "Jugador 3" };

        StartStandardGame(playerNames);

        Assert.Equal(3, _engine.Players.Count);
        Assert.All(_engine.Players, p => Assert.Equal(7, p.Hand.Count));
        Assert.NotNull(_engine.TopCard);
        Assert.Equal("Jugador 1", _engine.CurrentPlayer.Name);
    }

    [Fact]
    public void ReverseCard_InThreePlayers_ReversesRotationCorrectly()
    {
        StartStandardGame(new[] { "P1", "P2", "P3" });

        Assert.Equal(1, _engine.Direction);
        Assert.Equal("P1", _engine.CurrentPlayer.Name);

        var reverseCard = new Card(Guid.NewGuid(), _engine.ActiveColor, CardType.Reverse, null);
        _engine.CurrentPlayer.Hand.Add(reverseCard);

        _engine.PlayCard(reverseCard);

        Assert.Equal(-1, _engine.Direction);
        Assert.Equal("P3", _engine.CurrentPlayer.Name);
    }

    [Fact]
    public void SkipCard_SkipsNextPlayerInTurn()
    {
        StartStandardGame(new[] { "P1", "P2", "P3" });

        var skipCard = new Card(Guid.NewGuid(), _engine.ActiveColor, CardType.Skip, null);
        _engine.CurrentPlayer.Hand.Add(skipCard);

        _engine.PlayCard(skipCard);

        Assert.Equal("P3", _engine.CurrentPlayer.Name);
    }

    [Fact]
    public void DrawTwoCard_AddsTwoCardsToVictimAndSkipsTheirTurn()
    {
        StartStandardGame(new[] { "P1", "P2", "P3" });

        var p2InitialCount = _engine.Players[1].Hand.Count;
        var drawTwoCard = new Card(Guid.NewGuid(), _engine.ActiveColor, CardType.DrawTwo, null);
        _engine.CurrentPlayer.Hand.Add(drawTwoCard);

        _engine.PlayCard(drawTwoCard);

        Assert.Equal(p2InitialCount + 2, _engine.Players[1].Hand.Count);
        Assert.Equal("P3", _engine.CurrentPlayer.Name);
    }

    [Fact]
    public void PlayCard_WithoutCallingUno_PenalizesPlayerWithTwoCards()
    {
        StartStandardGame(new[] { "P1", "P2", "P3" });

        var p1 = _engine.CurrentPlayer;
        p1.Hand.Clear();

        var playableCard = new Card(Guid.NewGuid(), _engine.ActiveColor, CardType.Number, 1);
        var leftoverCard = new Card(Guid.NewGuid(), CardColor.Yellow, CardType.Number, 9);
        p1.Hand.Add(playableCard);
        p1.Hand.Add(leftoverCard);

        _engine.PlayCard(playableCard);

        // La regla de UNO penaliza al jugador anterior cuando el siguiente jugador realiza su jugada/robo
        _engine.DrawCard();

        Assert.Equal(3, p1.Hand.Count);
    }

    [Fact]
    public void PlayCard_WithCallUno_DoesNotPenalizePlayer()
    {
        StartStandardGame(new[] { "P1", "P2", "P3" });

        var p1 = _engine.CurrentPlayer;
        p1.Hand.Clear();

        var playableCard = new Card(Guid.NewGuid(), _engine.ActiveColor, CardType.Number, 1);
        var leftoverCard = new Card(Guid.NewGuid(), CardColor.Yellow, CardType.Number, 9);
        p1.Hand.Add(playableCard);
        p1.Hand.Add(leftoverCard);

        _engine.CallUno();
        _engine.PlayCard(playableCard);

        // El siguiente jugador realiza su turno; al haber cantado UNO, no se penaliza a P1
        _engine.DrawCard();

        Assert.Single(p1.Hand);
    }
}
