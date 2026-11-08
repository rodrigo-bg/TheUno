using System;

namespace TheUno.UI.Services;

/// <summary>
/// Compu donde corren el servidor del juego y la API de la base de datos. Se recuerda la de la última
/// partida para que el historial lea de ahí. Mientras no se haya jugado, es esta misma compu.
/// </summary>
public static class ServerAddress
{
    public const string Local = "127.0.0.1";
    public const int ApiPort = 8000;

    /// <summary>Solo la IP o el nombre de la compu, sin puerto.</summary>
    public static string Host { get; private set; } = Local;

    /// <summary>Dirección de la API en esa compu, por ejemplo http://192.168.1.50:8000/.</summary>
    public static Uri ApiUri => new($"http://{Host}:{ApiPort}/");

    /// <summary>
    /// Guarda la compu a la que se conectó la partida. Acepta lo mismo que se escribe al unirse:
    /// la IP, IP y puerto, o la dirección completa del hub.
    /// </summary>
    public static void Remember(string serverAddress)
    {
        var address = serverAddress.Trim();
        if (address.Length == 0) return;

        if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
        {
            Host = uri.Host;
            return;
        }

        var colon = address.IndexOf(':');
        Host = colon > 0 ? address[..colon] : address;
    }
}
