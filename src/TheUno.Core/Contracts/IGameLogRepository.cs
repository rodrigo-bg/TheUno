using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TheUno.Core.Contracts;

public interface IGameLogRepository
{
    Task LogActionAsync(GameLogEntryDto entry);
    Task<List<GameLogEntryDto>> GetLogsByMatchAsync(Guid matchId);
}