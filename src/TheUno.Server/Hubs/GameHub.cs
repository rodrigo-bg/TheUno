using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TheUno.Core.Models;
using TheUno.Server.Contracts;
using TheUno.Server.Services;

namespace TheUno.Server.Hubs;

/// <summary>
/// Hub central de SignalR que coordina la sala de espera y la partida en tiempo real.
/// Recibe los comandos de los clientes y difunde el estado personalizado a cada jugador.
/// </summary>
public class GameHub : Hub
{
    private readonly GameSessionManager _sessionManager;

    public GameHub(GameSessionManager sessionManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    // ==========================================
    // MÉTODOS DE SALA DE ESPERA (LOBBY)
    // ==========================================

    public Task CreateGame()
    {
        if (!_sessionManager.TryCreateGame(Context.ConnectionId))
        {
            throw new HubException("Ya hay una partida creada en este servidor.");
        }

        return Task.CompletedTask;
    }

    public Task<List<string>> GetTakenPlayers()
    {
        List<string> taken = _sessionManager.GetTakenPlayers();
        return Task.FromResult(taken);
    }

    public async Task<bool> ChoosePlayer(string playerName)
    {
        bool chosen = _sessionManager.ChoosePlayer(Context.ConnectionId, playerName);
        if (chosen)
        {
            List<string> taken = _sessionManager.GetTakenPlayers();
            await Clients.All.SendAsync(HubMessages.PlayersChanged, taken);
        }

        return chosen;
    }

    public async Task StartGame()
    {
        bool started = await _sessionManager.StartGameAsync();
        if (!started)
        {
            return;
        }

        // Primero se notifica que la partida inicia y enseguida se envía la mesa individual a cada cliente
        await Clients.All.SendAsync(HubMessages.GameStarted);
        await BroadcastPersonalizedStatesAsync();
    }

    // ==========================================
    // MÉTODOS DE JUEGO EN TIEMPO REAL
    // ==========================================

    public async Task PlayCard(Guid cardId, CardColor? chosenColor = null)
    {
        bool played = _sessionManager.TryPlayCard(
            Context.ConnectionId,
            cardId,
            chosenColor,
            out string? unoWindowPlayerName,
            out string? penaltyPlayerName,
            out (string WinnerName, double DurationSeconds)? gameOverInfo
        );

        if (!played)
        {
            return;
        }

        if (penaltyPlayerName != null)
        {
            await Clients.All.SendAsync(HubMessages.UnoPenalty, penaltyPlayerName);
        }

        await BroadcastPersonalizedStatesAsync();

        if (unoWindowPlayerName != null)
        {
            await Clients.All.SendAsync(
                HubMessages.UnoWindowOpened,
                unoWindowPlayerName,
                GameSessionManager.UnoWindowMilliseconds
            );
        }

        if (gameOverInfo.HasValue)
        {
            await Clients.All.SendAsync(
                HubMessages.GameOver,
                gameOverInfo.Value.WinnerName,
                gameOverInfo.Value.DurationSeconds
            );
            _sessionManager.ClearFinishedGame();
        }
    }

    public async Task DrawCard()
    {
        bool drawn = _sessionManager.TryDrawCard(Context.ConnectionId, out string? penaltyPlayerName);
        if (!drawn)
        {
            return;
        }

        if (penaltyPlayerName != null)
        {
            await Clients.All.SendAsync(HubMessages.UnoPenalty, penaltyPlayerName);
        }

        await BroadcastPersonalizedStatesAsync();
    }

    public async Task PassTurn()
    {
        bool passed = _sessionManager.TryPassTurn(Context.ConnectionId);
        if (!passed)
        {
            return;
        }

        await BroadcastPersonalizedStatesAsync();
    }

    public async Task CallUno()
    {
        bool called = _sessionManager.TryCallUno(Context.ConnectionId);
        if (!called)
        {
            return;
        }

        await BroadcastPersonalizedStatesAsync();
    }

    // ==========================================
    // SALIDA Y DESCONEXIÓN
    // ==========================================

    public async Task LeaveGame()
    {
        await HandlePlayerExitAsync(Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await HandlePlayerExitAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task HandlePlayerExitAsync(string connectionId)
    {
        _sessionManager.RemoveConnection(connectionId, out bool wasGameInterrupted, out string? leftPlayerName);

        if (wasGameInterrupted && leftPlayerName != null)
        {
            await Clients.All.SendAsync(HubMessages.PlayerLeft, leftPlayerName);
        }
        else if (leftPlayerName != null)
        {
            List<string> taken = _sessionManager.GetTakenPlayers();
            await Clients.All.SendAsync(HubMessages.PlayersChanged, taken);
        }
    }

    private async Task BroadcastPersonalizedStatesAsync()
    {
        List<(string ConnectionId, GameStateDto State)> states = _sessionManager.CreatePersonalizedStates();
        for (int i = 0; i < states.Count; i++)
        {
            string connectionId = states[i].ConnectionId;
            GameStateDto state = states[i].State;
            await Clients.Client(connectionId).SendAsync(HubMessages.StateChanged, state);
        }
    }
}
