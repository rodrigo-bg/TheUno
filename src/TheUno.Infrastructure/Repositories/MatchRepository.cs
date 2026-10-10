using Microsoft.EntityFrameworkCore;
using TheUno.Core.Contracts;
using TheUno.Infrastructure.Entities;

namespace TheUno.Infrastructure.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly UnoDbContext _context;

    public MatchRepository(UnoDbContext context)
    {
        _context = context;
    }
    
    /// Registra el inicio de una nueva partida con un nuevo Guid.
    public async Task<Guid> CreateMatchAsync(IEnumerable<Guid> playerIds)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            TotalTurns = 0
        };
        
        // Agrega la relación con los 3 jugadores
        foreach (var playerId in playerIds)
        {
            match.MatchPlayers.Add(new MatchPlayer
            {
                MatchId = match.Id,
                PlayerId = playerId,
            });  
        }

        _context.Matches.Add(match);
        await _context.SaveChangesAsync();

        return match.Id;
    }
    
    /// Finaliza la partida estableciendo el ganador, los turnos totales y la duración.
    public async Task CompleteMatchAsync(Guid matchId, Guid winnerId, int totalTurns, TimeSpan duration)
    {
        var match = await _context.Matches.FindAsync(matchId);
        if (match != null)
        {
            match.EndedAt = DateTime.UtcNow;
            match.WinnerId = winnerId;
            match.TotalTurns = totalTurns;
            match.Duration = duration;

            await _context.SaveChangesAsync();
        }
    }
    
    /// Genera las estadísticas para la tabla de clasificación (Leaderboard) mediante consultas LINQ.
    public async Task<List<PlayerStatsDto>> GetLeaderboardStatsAsync()
    {
        var completedMatches = await _context.Matches
            .Where(m => m.EndedAt != null)
            .ToListAsync();

        var totalCompletedMatches = completedMatches.Count;
        double avgDurationMinutes = totalCompletedMatches > 0 
            ? completedMatches.Average(m => m.Duration.TotalMinutes) 
            : 0.0;

        var stats = await _context.Players
            .Select(p => new PlayerStatsDto(
                p.Id,
                p.Name,
                totalCompletedMatches,
                p.WonMatches.Count(m => m.EndedAt != null),
                totalCompletedMatches - p.WonMatches.Count(m => m.EndedAt != null),
                Math.Round(avgDurationMinutes, 2),
                p.GameLogs.Where(g => g.Action == GameActionType.GameOver).Sum(g => 0) // Se puede enriquecer o sumar puntos de victoria si se registran en CardDetails
            ))
            .ToListAsync();

        return stats;
    }
}