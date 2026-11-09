namespace TheUno.Server.Contracts;

/// <summary>
/// Nombres de los métodos remotos expuestos por el GameHub que los clientes pueden invocar.
/// </summary>
public static class HubMethods
{
    public const string CreateGame = "CreateGame";
    public const string GetTakenPlayers = "GetTakenPlayers";
    public const string ChoosePlayer = "ChoosePlayer";
    public const string StartGame = "StartGame";
    public const string PlayCard = "PlayCard";
    public const string DrawCard = "DrawCard";
    public const string PassTurn = "PassTurn";
    public const string CallUno = "CallUno";
    public const string LeaveGame = "LeaveGame";
}
