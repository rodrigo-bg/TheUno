using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

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
    private SignalRGameSession? _session;

    // El servidor ya avisó que empieza, pero todavía no llega la primera mesa
    private bool _isStarting;

    public IReadOnlyList<string> JoinedPlayers => _joinedPlayers;

    public IGameSession? Session => _session;

    public event Action? PlayersChanged;
    public event Action? GameStarted;

    public async Task CreateGameAsync()
    {
        if (!_connection.IsConnected) await ConnectAsync(LocalServer);

        try
        {
            await _connection.InvokeAsync(HubMethods.CreateGame);
        }
        catch (HubException e)
        {
            // CreateGame solo se rechaza cuando ya hay una partida en el servidor
            throw new GameAlreadyExistsException(e);
        }
    }

    public async Task ConnectAsync(string serverIp)
    {
        await _connection.ConnectAsync(serverIp);
        _connection.On<List<string>>(HubMessages.PlayersChanged, SetJoinedPlayers);
        _connection.On(HubMessages.GameStarted, () => _isStarting = true);

        _session = new SignalRGameSession(_connection);
        _session.StateChanged += OnStateChanged;

        // Pueden haber llegado otros antes que esta compu
        SetJoinedPlayers(await _connection.InvokeAsync<List<string>>(HubMethods.GetTakenPlayers));
    }

    public async Task<bool> ChoosePlayerAsync(string playerName)
    {
        var chosen = await _connection.InvokeAsync<bool>(HubMethods.ChoosePlayer, playerName);
        if (chosen)
        {
            SetJoinedPlayers(await _connection.InvokeAsync<List<string>>(HubMethods.GetTakenPlayers));
        }

        return chosen;
    }

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

    // Se avisa con la primera mesa ya en la sesión, para que el tablero nunca abra vacío
    private void OnStateChanged(GameStateDto state)
    {
        if (!_isStarting) return;

        _isStarting = false;
        GameStarted?.Invoke();
    }
}
