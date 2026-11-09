using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheUno.Core.Contracts;

namespace TheUno.Tests.Fakes;

public class FakeGameLogRepository : IGameLogRepository
{
    public List<GameLogEntryDto> Entries { get; } = new();

    public Task LogActionAsync(GameLogEntryDto entry)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<List<GameLogEntryDto>> GetLogsByMatchAsync(Guid matchId)
    {
        var logs = Entries.Where(e => e.MatchId == matchId).ToList();
        return Task.FromResult(logs);
    }
}