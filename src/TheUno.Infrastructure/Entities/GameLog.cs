namespace TheUno.Infrastructure.Entities;

public class GameLog
{   
    // Atributos de la entidad 'GameLog'
    public long Id {get; set;}
    
    // Relación con la partida
    // Una partida genera muchos registros de auditoría Match 1 -> N GameLog
    public int MatchId {get; set;}
    public Match Match {get; set;} = null!;

    public int TurnNumber {get; set;}
    
    // Relación con el jugador que realizó la acción
    // Un jugador genera muchos logs en las partidas que juega Player 1 -> N GameLog
    public int PlayerId {get; set;}
    public Player Player { get; set; } = null!;
    
    // Tipo de acción: "DRAW", "PLAY_CARD", "PASS", "UNO_PENALTY"
    public string ActionType {get; set;} = string.Empty;
    
    // Detalles de la carta (ej. "Red_7", "Wild_DrawFour")
    public string? CardDetails {get; set;}
    
    // Color elegido en caso de un comodín (ej. "Red", "Blue")
    public string? ColorChosen {get; set;}
    
    public DateTime Timetamp {get; set;} = DateTime.UtcNow;
}