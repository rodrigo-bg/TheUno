using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TheUno.UI.Services;

/// <summary>
/// Sala de conexión real: habla con el GameHub del servidor por medio de GameConnection.
/// Quien crea la partida se conecta al servidor que corre en su propia compu.
/// </summary>
public sealed class SignalRLobbyService : ILobbyService
{
    // 127.0.0.1 en lugar de localhost: en Windows localhost también intenta IPv6 y tarda más en fallar
    private const string LocalServer = "127.0.0.1";

    private readonly GameConnection _connection = new();
    private List<string> _joinedPlayers = new();

    public IReadOnlyList<string> JoinedPlayers => _joinedPlayers;

    public event Action? PlayersChanged;
    public event Action? GameStarted;

    public async Task CreateGameAsync()
    {
        if (!_connection.IsConnected) await ConnectAsync(LocalServer);
        await _connection.InvokeAsync(HubMethods.CreateGame);
    }

    public async Task ConnectAsync(string serverIp)
    {
        await _connection.ConnectAsync(serverIp);
        _connection.On<List<string>>(HubMessages.PlayersChanged, SetJoinedPlayers);
        _connection.On(HubMessages.GameStarted, () => GameStarted?.Invoke());

        // Pueden haber llegado otros antes que esta compu
        SetJoinedPlayers(await _connection.InvokeAsync<List<string>>(HubMethods.GetTakenPlayers));
    }

    public Task<bool> ChoosePlayerAsync(string playerName) =>
        _connection.InvokeAsync<bool>(HubMethods.ChoosePlayer, playerName);

    public Task StartGameAsync() => _connection.InvokeAsync(HubMethods.StartGame);

    public async Task LeaveAsync()
    {
        try
        {
            if (_connection.IsConnected) await _connection.InvokeAsync(HubMethods.LeaveGame);
        }
        finally
        {
            await _connection.DisconnectAsync();
        }
    }

    private void SetJoinedPlayers(List<string> players)
    {
        _joinedPlayers = players;
        PlayersChanged?.Invoke();
    }
}
