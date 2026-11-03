using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TheUno.UI.Services;

/// <summary>Datos de red de esta compu, para mostrar la IP que los demás escriben al unirse.</summary>
public static class NetworkInfo
{
    /// <summary>
    /// IP de esta compu en la red local (ej. "192.168.1.50"), o null si no está conectada a ninguna red.
    /// </summary>
    public static string? GetLocalIPv4()
    {
        // Un socket UDP "conectado" no manda nada, pero el sistema elige la tarjeta de red
        // que usaría para salir: esa es la IP que ven las demás compus.
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint { Address: var address } && !IPAddress.IsLoopback(address))
            {
                return address.ToString();
            }
        }
        catch (SocketException)
        {
            // Sin ruta hacia afuera: se busca a mano entre las tarjetas de red
        }

        // Tarjetas encendidas con puerta de enlace (las virtuales normalmente no tienen)
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.GetIPProperties())
            .Where(p => p.GatewayAddresses.Any(g => !g.Address.Equals(IPAddress.Any)))
            .SelectMany(p => p.UnicastAddresses)
            .Select(a => a.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?.ToString();
    }
}
