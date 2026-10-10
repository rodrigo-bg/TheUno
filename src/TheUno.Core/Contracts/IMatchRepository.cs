namespace TheUno.Core.Contracts;

public record PlayerStatsDto(
    Guid PlayerId,
    string Name,
    int MatchesPlayed,
    int MatchesWon,
    int MatchesLost,
    double AverageDurationMinutes,
    int TotalScore
);

// Resumen de una partida terminada, para la lista del historial
public record MatchSummaryDto(
    Guid MatchId,
    DateTime StartedAt,
    string WinnerName,
    double DurationMinutes,
    int TotalTurns
);

public interface IMatchRepository
{
    Task<Guid> CreateMatchAsync(IEnumerable<Guid> playerIds);
    Task CompleteMatchAsync(Guid matchId, Guid winnerId, int totalTurns, TimeSpan duration);
    Task<List<PlayerStatsDto>> GetLeaderboardStatsAsync();
    Task<List<MatchSummaryDto>> GetCompletedMatchesAsync(); // De la más reciente a la más antigua
}