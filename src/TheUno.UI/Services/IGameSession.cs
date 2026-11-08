using System;
using System.Threading.Tasks;
using TheUno.Core.Models;

namespace TheUno.UI.Services;

/// <summary>
/// Una partida en curso vista desde el tablero. Las reglas no viven aquí: el tablero manda
/// lo que quiere hacer el jugador y dibuja lo que le contestan.
/// Los eventos se lanzan en el hilo de la interfaz.
/// </summary>
public interface IGameSession
{
    /// <summary>La mesa más reciente, o null si todavía no llega la primera.</summary>
    GameStateDto? State { get; }

    /// <summary>Cambió algo en la mesa.</summary>
    event Action<GameStateDto>? StateChanged;

    /// <summary>Un jugador tiró su penúltima carta: tiene estos milisegundos para cantar UNO.</summary>
    event Action<string, int>? UnoWindowOpened;

    /// <summary>Un jugador no cantó UNO y recibió 2 cartas.</summary>
    event Action<string>? UnoPenalty;

    /// <summary>Alguien ganó, con la duración de la partida.</summary>
    event Action<string, TimeSpan>? GameOver;

    /// <summary>Un jugador se salió y la partida ya no puede seguir.</summary>
    event Action<string>? PlayerLeft;

    /// <summary>Se perdió la conexión con el servidor.</summary>
    event Action? ConnectionLost;

    /// <summary>Tira una carta. Los comodines llevan el color elegido.</summary>
    Task PlayCardAsync(Guid cardId, CardColor? chosenColor = null);

    /// <summary>Roba una carta del mazo.</summary>
    Task DrawCardAsync();

    /// <summary>Se queda con la carta robada y pasa el turno.</summary>
    Task PassTurnAsync();

    /// <summary>Canta UNO dentro de la ventana de 2 segundos.</summary>
    Task CallUnoAsync();

    /// <summary>Sale de la partida.</summary>
    Task LeaveAsync();
}
