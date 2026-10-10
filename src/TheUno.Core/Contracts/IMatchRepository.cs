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

public interface IMatchRepository
{
    Task<Guid> CreateMatchAsync(IEnumerable<Guid> playerIds);
    Task CompleteMatchAsync(Guid matchId, Guid winnerId, int totalTurns, TimeSpan duration);
    Task<List<PlayerStatsDto>> GetLeaderboardStatsAsync();
}