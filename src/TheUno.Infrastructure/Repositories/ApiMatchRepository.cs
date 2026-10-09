using System.Net.Http.Json;
using TheUno.Core.Contracts;

namespace TheUno.Infrastructure.Repositories;

public class ApiMatchRepository : IMatchRepository
{
    private readonly HttpClient _httpClient;

    public ApiMatchRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Guid> CreateMatchAsync(IEnumerable<Guid> playerIds)
    {
        var payload = new
        {
            player_ids = playerIds.Select(id => id.ToString()).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync("matches", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MatchCreatedResponse>();
        return result?.MatchId ?? Guid.Empty;
    }

    public async Task CompleteMatchAsync(Guid matchId, Guid winnerId, int totalTurns, TimeSpan duration)
    {
        var payload = new
        {
            winner_id = winnerId.ToString(),
            total_turns = totalTurns,
            duration = duration.ToString(@"hh\:mm\:ss")
        };

        var response = await _httpClient.PostAsJsonAsync($"matches/{matchId}/complete", payload);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<PlayerStatsDto>> GetLeaderboardStatsAsync()
    {
        var stats = await _httpClient.GetFromJsonAsync<List<PlayerStatsDto>>("players/stats");
        return stats ?? new List<PlayerStatsDto>();
    }

    private record MatchCreatedResponse(Guid MatchId, string StartedAt);
}