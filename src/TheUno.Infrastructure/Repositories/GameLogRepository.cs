using Microsoft.EntityFrameworkCore;
using TheUno.Core.Contracts;
using TheUno.Infrastructure.Entities;

namespace TheUno.Infrastructure.Repositories;

public class GameLogRepository : IGameLogRepository
{
    private readonly UnoDbContext _context;

    public GameLogRepository(UnoDbContext context)
    {
        _context = context;
    }
    
    // Guarda un registro de auditoría de manera asíncrona.
    public async Task LogActionAsync(GameLogEntryDto entry)
    {
        var log = new GameLog
        {
            MatchId = entry.MatchId,
            TurnNumber = entry.TurnNumber,
            PlayerId = entry.PlayerId,
            Action = entry.Action,
            CardDetails = entry.CardDetails,
            ColorChosen = entry.ChosenColor,
            Timestamp = entry.Timestamp
        };

        _context.GameLogs.Add(log);
        await _context.SaveChangesAsync();
    }
    
    // Consulta y recupera toda la traza de movimientos de una partida finalizada.
    public async Task<List<GameLogEntryDto>> GetLogsByMatchAsync(Guid matchId)
    {
        return await _context.GameLogs
            .Where(g => g.MatchId == matchId)
            .OrderBy(g => g.TurnNumber)
            .ThenBy(g => g.Timestamp)
            .Select(g => new GameLogEntryDto(
                g.MatchId,
                g.TurnNumber,
                g.PlayerId,
                g.Action,
                g.CardDetails,
                g.ColorChosen,
                g.Timestamp
            ))
            .ToListAsync();
    }
}