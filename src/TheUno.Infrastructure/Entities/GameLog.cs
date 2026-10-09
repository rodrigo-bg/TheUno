using TheUno.Core.Contracts;

namespace TheUno.Infrastructure.Entities;

public class GameLog
{   
    // Atributos de la entidad 'GameLog'
    public long Id {get; set;}
    
    // Relación con la partida
    // Una partida genera muchos registros de auditoría Match 1 -> N GameLog
    public Guid MatchId {get; set;}
    public Match Match {get; set;} = null!;

    public int TurnNumber {get; set;}
    
    // Relación con el jugador que realizó la acción
    // Un jugador genera muchos logs en las partidas que juega Player 1 -> N GameLog
    public Guid PlayerId {get; set;}
    public Player Player { get; set; } = null!;
    
    // Tipo de acción: "DRAW", "PLAY_CARD", "PASS", "UNO_PENALTY"
    public GameActionType Action {get; set;}
    
    // Detalles de la carta (ej. "Red_7", "Wild_DrawFour")
    public string CardDetails {get; set;} = string.Empty;
    
    // Color elegido en caso de un comodín (ej. "Red", "Blue")
    public string? ColorChosen {get; set;}
    
    public DateTime Timestamp {get; set;} = DateTime.UtcNow;
}