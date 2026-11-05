namespace TheUno.Server.Contracts;

/// <summary>
/// Nombres de los eventos en tiempo real que el servidor emite hacia los clientes conectados.
/// </summary>
public static class HubMessages
{
    public const string PlayersChanged = "PlayersChanged";
    public const string GameStarted = "GameStarted";
    public const string StateChanged = "StateChanged";
    public const string UnoWindowOpened = "UnoWindowOpened";
    public const string UnoPenalty = "UnoPenalty";
    public const string GameOver = "GameOver";
    public const string PlayerLeft = "PlayerLeft";
}
