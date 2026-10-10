using System.Net.Http.Json;
using System.Text.Json;
using TheUno.Core.Contracts;

namespace TheUno.Infrastructure.Repositories;

public class ApiMatchRepository : IMatchRepository
{
    private readonly HttpClient _httpClient;

    // La API en Python responde en snake_case (match_id, matches_played...)
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

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

        var result = await response.Content.ReadFromJsonAsync<MatchCreatedResponse>(JsonOptions);
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
        var stats = await _httpClient.GetFromJsonAsync<List<PlayerStatsDto>>("players/stats", JsonOptions);
        return stats ?? new List<PlayerStatsDto>();
    }

    public async Task<List<MatchSummaryDto>> GetCompletedMatchesAsync()
    {
        var matches = await _httpClient.GetFromJsonAsync<List<MatchSummaryDto>>("matches", JsonOptions);
        return matches ?? new List<MatchSummaryDto>();
    }

    private record MatchCreatedResponse(Guid MatchId, string StartedAt);
}