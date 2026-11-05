using System;
using System.Linq;
using System.Threading.Tasks;
using TheUno.Server.Services;
using TheUno.Tests.Fakes;
using Xunit;

namespace TheUno.Tests;

public class GameSessionManagerTests
{
    private readonly FakeMatchRepository _matchRepo;
    private readonly FakeGameLogRepository _logRepo;
    private readonly GameSessionManager _manager;

    public GameSessionManagerTests()
    {
        _matchRepo = new FakeMatchRepository();
        _logRepo = new FakeGameLogRepository();
        _manager = new GameSessionManager(_matchRepo, _logRepo);
    }

    [Fact]
    public void ChoosePlayer_RejectsDuplicateSelectionFromDifferentConnections()
    {
        _manager.CreateGame("conn-host");

        bool firstPick = _manager.ChoosePlayer("conn-host", "RODRIGO");
        bool duplicatePick = _manager.ChoosePlayer("conn-2", "RODRIGO");

        Assert.True(firstPick);
        Assert.False(duplicatePick);
        Assert.Single(_manager.GetTakenPlayers());
    }

    [Fact]
    public async Task CreatePersonalizedStates_SendsOnlyOwnHandToEachPlayer()
    {
        _manager.CreateGame("conn-1");
        _manager.ChoosePlayer("conn-1", "RODRIGO");
        _manager.ChoosePlayer("conn-2", "TONATIUH");
        _manager.ChoosePlayer("conn-3", "ALEJANDRO");

        bool started = await _manager.StartGameAsync();
        Assert.True(started);

        var states = _manager.CreatePersonalizedStates();
        Assert.Equal(3, states.Count);

        var rodriState = states.First(s => s.ConnectionId == "conn-1").State;
        var tonaState = states.First(s => s.ConnectionId == "conn-2").State;
        var aleState = states.First(s => s.ConnectionId == "conn-3").State;

        Assert.Equal("RODRIGO", rodriState.MyName);
        Assert.Equal("TONATIUH", tonaState.MyName);
        Assert.Equal("ALEJANDRO", aleState.MyName);

        // Las cartas en MyHand de cada jugador tienen Ids únicos que no deben estar en la mano de los demás
        var rodriCardIds = rodriState.MyHand.Select(c => c.Id).ToHashSet();
        var tonaCardIds = tonaState.MyHand.Select(c => c.Id).ToHashSet();
        var aleCardIds = aleState.MyHand.Select(c => c.Id).ToHashSet();

        Assert.Empty(rodriCardIds.Intersect(tonaCardIds));
        Assert.Empty(rodriCardIds.Intersect(aleCardIds));
        Assert.Empty(tonaCardIds.Intersect(aleCardIds));

        // De los rivales solo viaja el conteo de cartas
        Assert.All(rodriState.Players, p => Assert.True(p.CardCount >= 7));
    }

    [Fact]
    public async Task RemoveConnection_DuringActiveGame_MarksGameAsInterrupted()
    {
        _manager.CreateGame("conn-1");
        _manager.ChoosePlayer("conn-1", "RODRIGO");
        _manager.ChoosePlayer("conn-2", "TONATIUH");
        _manager.ChoosePlayer("conn-3", "ALEJANDRO");
        await _manager.StartGameAsync();

        _manager.RemoveConnection("conn-2", out bool wasGameInterrupted, out string? leftPlayerName);

        Assert.True(wasGameInterrupted);
        Assert.Equal("TONATIUH", leftPlayerName);
    }
}
