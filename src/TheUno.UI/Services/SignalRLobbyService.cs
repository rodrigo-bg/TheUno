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
    private readonly GameConnection _connection = new();
    private List<string> _joinedPlayers = new();
    private SignalRGameSession? _session;

    // El servidor ya avisó que empieza, pero todavía no llega la primera mesa
    private bool _isStarting;

    public IReadOnlyList<string> JoinedPlayers => _joinedPlayers;

    public IGameSession? Session => _session;

    public event Action? PlayersChanged;
    public event Action? GameStarted;

    /// <summary>Se conecta al servidor de esta misma compu y abre la partida.</summary>
    public async Task CreateGameAsync()
    {
        if (!_connection.IsConnected) await ConnectAsync(ServerAddress.Local);

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

    /// <summary>Se conecta, escucha la sala y prepara la sesión antes de que llegue la primera mesa.</summary>
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

    public Task<bool> ChoosePlayerAsync(string playerName) =>
        _connection.InvokeAsync<bool>(HubMethods.ChoosePlayer, playerName);

    public Task StartGameAsync() => _connection.InvokeAsync(HubMethods.StartGame);

    /// <summary>Avisa al servidor que se va y cierra la conexión, aunque el aviso falle.</summary>
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
