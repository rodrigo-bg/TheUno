using System;
using System.Threading.Tasks;
using TheUno.Core.Models;

namespace TheUno.UI.Services;

/// <summary>
/// Partida real: cada jugada se le pide al GameHub y todo lo que pasa en la mesa llega como mensaje.
/// Se crea antes de que empiece la partida para no perder el primer estado.
/// </summary>
public sealed class SignalRGameSession : IGameSession
{
    private readonly GameConnection _connection;

    public SignalRGameSession(GameConnection connection)
    {
        _connection = connection;

        connection.ConnectionLost += () => ConnectionLost?.Invoke();
        connection.On<GameStateDto>(HubMessages.StateChanged, OnStateChanged);
        connection.On<string, int>(HubMessages.UnoWindowOpened, (name, time) => UnoWindowOpened?.Invoke(name, time));
        connection.On<string>(HubMessages.UnoPenalty, name => UnoPenalty?.Invoke(name));
        connection.On<string, double>(HubMessages.GameOver,
            (winner, seconds) => GameOver?.Invoke(winner, TimeSpan.FromSeconds(seconds)));
        connection.On<string>(HubMessages.PlayerLeft, name => PlayerLeft?.Invoke(name));
    }

    public GameStateDto? State { get; private set; }

    public event Action<GameStateDto>? StateChanged;
    public event Action<string, int>? UnoWindowOpened;
    public event Action<string>? UnoPenalty;
    public event Action<string, TimeSpan>? GameOver;
    public event Action<string>? PlayerLeft;
    public event Action? ConnectionLost;

    public Task PlayCardAsync(Guid cardId, CardColor? chosenColor = null) =>
        _connection.InvokeAsync(HubMethods.PlayCard, cardId, chosenColor);

    public Task DrawCardAsync() => _connection.InvokeAsync(HubMethods.DrawCard);

    public Task PassTurnAsync() => _connection.InvokeAsync(HubMethods.PassTurn);

    public Task CallUnoAsync() => _connection.InvokeAsync(HubMethods.CallUno);

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

    private void OnStateChanged(GameStateDto state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
