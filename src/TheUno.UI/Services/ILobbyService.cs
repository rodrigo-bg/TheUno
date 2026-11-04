using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TheUno.UI.Services;

/// <summary>
/// Sala de conexión de una partida en red: crearla en esta compu o unirse a la de otra,
/// elegir jugador y empezar cuando ya están los 3. Las pantallas solo hablan con esta interfaz,
/// así no les importa si del otro lado está el servidor real o una simulación.
/// Los eventos se lanzan en el hilo de la interfaz.
/// </summary>
public interface ILobbyService
{
    /// <summary>Jugadores que ya eligieron nombre, en el orden en que se unieron.</summary>
    IReadOnlyList<string> JoinedPlayers { get; }

    /// <summary>La partida de esta sala. Cuando llega GameStarted ya trae la primera mesa.</summary>
    IGameSession? Session { get; }

    /// <summary>Alguien se unió a la sala o se salió de ella.</summary>
    event Action? PlayersChanged;

    /// <summary>Quien creó la partida la empezó: todos pasan al tablero.</summary>
    event Action? GameStarted;

    /// <summary>Esta compu crea la partida y queda como servidor.</summary>
    Task CreateGameAsync();

    /// <summary>Se conecta a la partida de otra compu. Lanza una excepción si no responde.</summary>
    Task ConnectAsync(string serverIp);

    /// <summary>Elige con qué jugador entrar. Regresa false si alguien más ya lo eligió.</summary>
    Task<bool> ChoosePlayerAsync(string playerName);

    /// <summary>Empieza la partida. Solo lo puede hacer quien la creó, con los 3 jugadores unidos.</summary>
    Task StartGameAsync();

    /// <summary>Sale de la sala (o la cierra, si es quien la creó).</summary>
    Task LeaveAsync();
}
