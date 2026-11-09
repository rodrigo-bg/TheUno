using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TheUno.Core.Contracts;

namespace TheUno.Tests.Fakes;

public class FakeMatchRepository : IMatchRepository
{
    public Guid LastCreatedMatchId { get; private set; }
    public Guid? CompletedWinnerId { get; private set; }

    public Task<Guid> CreateMatchAsync(IEnumerable<Guid> playerIds)
    {
        LastCreatedMatchId = Guid.NewGuid();
        return Task.FromResult(LastCreatedMatchId);
    }

    public Task CompleteMatchAsync(Guid matchId, Guid winnerId, int totalTurns, TimeSpan duration)
    {
        CompletedWinnerId = winnerId;
        return Task.CompletedTask;
    }

    public Task<List<PlayerStatsDto>> GetLeaderboardStatsAsync()
    {
        return Task.FromResult(new List<PlayerStatsDto>());
    }

    public Task<List<MatchSummaryDto>> GetCompletedMatchesAsync()
    {
        return Task.FromResult(new List<MatchSummaryDto>());
    }
}
