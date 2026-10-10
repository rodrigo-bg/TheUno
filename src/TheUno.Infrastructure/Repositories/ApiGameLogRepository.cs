using System.Net.Http.Json;
using TheUno.Core.Contracts;

namespace TheUno.Infrastructure.Repositories;

public class ApiGameLogRepository : IGameLogRepository
{
    private readonly HttpClient _httpClient;

    public ApiGameLogRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task LogActionAsync(GameLogEntryDto entry)
    {
        var payload = new
        {
            match_id = entry.MatchId.ToString(),
            turn_number = entry.TurnNumber,
            player_id = entry.PlayerId.ToString(),
            action = entry.Action.ToString(),
            card_details = entry.CardDetails,
            chosen_color = entry.ChosenColor,
            timestamp = entry.Timestamp.ToString("o")
        };

        var response = await _httpClient.PostAsJsonAsync("logs", payload);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<GameLogEntryDto>> GetLogsByMatchAsync(Guid matchId)
    {
        var logs = await _httpClient.GetFromJsonAsync<List<GameLogEntryDto>>($"matches/{matchId}/logs");
        return logs ?? new List<GameLogEntryDto>();
    }
}