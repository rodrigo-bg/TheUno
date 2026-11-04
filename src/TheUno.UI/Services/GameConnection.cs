using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;

namespace TheUno.UI.Services;

/// <summary>
/// Conexión con el servidor de la partida (SignalR). El cliente le manda sus jugadas
/// y recibe de él todo el estado del juego; aquí no hay motor local.
/// </summary>
public sealed class GameConnection : IAsyncDisposable
{
    public const int DefaultPort = 5000;
    public const string HubPath = "/gamehub";

    // Si la IP no responde en este tiempo se avisa al jugador en lugar de esperar al sistema
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private HubConnection? _connection;

    /// <summary>Se perdió la conexión con el servidor (se cerró o se cayó la red). Llega en el hilo de la interfaz.</summary>
    public event Action? ConnectionLost;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Se conecta al servidor. Acepta solo la IP ("192.168.1.50"), IP y puerto ("192.168.1.50:5000")
    /// o la dirección completa ("http://192.168.1.50:5000/gamehub").
    /// Lanza una excepción si el servidor no responde.
    /// </summary>
    public async Task ConnectAsync(string serverAddress)
    {
        await DisconnectAsync();

        var connection = new HubConnectionBuilder()
            .WithUrl(BuildHubUrl(serverAddress))
            .Build();

        // Al desconectarse a propósito _connection ya es null: eso no cuenta como conexión perdida.
        connection.Closed += _ =>
        {
            if (_connection == connection) Dispatcher.UIThread.Post(() => ConnectionLost?.Invoke());
            return Task.CompletedTask;
        };

        // Se guarda antes de conectar para que los mensajes se puedan escuchar desde el primero
        _connection = connection;

        using var timeout = new CancellationTokenSource(ConnectTimeout);
        try
        {
            await connection.StartAsync(timeout.Token);
        }
        catch
        {
            _connection = null;
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>Llama un método del servidor (por ejemplo, tirar una carta).</summary>
    public Task InvokeAsync(string method, params object?[] args) =>
        GetConnection().InvokeCoreAsync(method, args);

    /// <summary>Llama un método del servidor que regresa una respuesta.</summary>
    public Task<T> InvokeAsync<T>(string method, params object?[] args) =>
        GetConnection().InvokeCoreAsync<T>(method, args);

    /// <summary>
    /// Escucha un mensaje del servidor. El manejador corre en el hilo de la interfaz,
    /// así puede cambiar propiedades de los ViewModels directamente.
    /// Para dejar de escuchar se hace Dispose del resultado.
    /// </summary>
    public IDisposable On<T>(string message, Action<T> handler) =>
        GetConnection().On<T>(message, value => Dispatcher.UIThread.Post(() => handler(value)));

    public async Task DisconnectAsync()
    {
        var connection = _connection;
        _connection = null;
        if (connection is not null) await connection.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(DisconnectAsync());

    private HubConnection GetConnection() =>
        _connection ?? throw new InvalidOperationException("No hay conexión con el servidor.");

    private static string BuildHubUrl(string serverAddress)
    {
        var address = serverAddress.Trim();
        if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return address;
        }

        if (!address.Contains(':')) address += $":{DefaultPort}";
        return $"http://{address}{HubPath}";
    }
}
