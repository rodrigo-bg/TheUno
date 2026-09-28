// using TheUno.Core.Repositories; Esperando la implementación
using Microsoft.EntityFrameworkCore;
using TheUno.Infrastructure.Entities;

namespace TheUno.Infrastructure.Repositories;

public class MatchRepository //: IMatchRepository
{
    private readonly UnoDbContext _context;

    public MatchRepository(UnoDbContext context)
    {
        _context = context;
    }
    
    // Guardar el inicio de una partida
    public async Task<int> CreateMatchAsync()
    {
        var match = new Match
        {
            StartedAt = DateTime.UtcNow,
            TotalTurns = 0
        };

        _context.Matches.Add(match);
        await _context.SaveChangesAsync();
        return match.Id;
    }
    
    // Cerrar la partida al finalizar
    public async Task CompleteMatchAsync(int matchId, int winnerId, int totalTurns)
    {
        var match = await _context.Matches.FindAsync(matchId);
        if (match != null)
        {
            match.EndedAt = DateTime.UtcNow;
            match.WinnerId = winnerId;
            match.TotalTurns = totalTurns;
            await _context.SaveChangesAsync();
        }
    }
    
    // Consulta LINQ para la table de clasificación (Leaderboard(Estadísticas)
    public async Task<List<PlayerStatsDto>> GetPlayerStatsAsync()
    {
        var totalMatches = await _context.Matches.CountAsync(m => m.EndedAt != null);

        var stats = await _context.Players.Select(p => new PlayerStatsDto
            {
                PlayerId = p.Id,
                PlayerName = p.Name,
                GamesWon = p.WonMatches.Count,
                TotalGames = totalMatches,
                WinRate = totalMatches > 0 ? (double)p.WonMatches.Count / totalMatches * 100 : 0
            })
            .ToListAsync();
        return stats;
    }
}

// DTO simple para transferir las estadpisticas a la vista de Tona
public class PlayerStatsDto
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int GamesWon { get; set; }
    public int TotalGames { get; set; }
    public double WinRate { get; set; }
}