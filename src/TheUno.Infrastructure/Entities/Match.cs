namespace TheUno.Infrastructure.Entities;

public class Match
{
    // Atributos de la entidad 'Match'
    public int Id {get; set;}
    public DateTime StartedAt {get; set;} = DateTime.UtcNow;
    public DateTime? EndedAt {get; set;}
    public int TotalTurns {get; set;} = 0;
    
    // Clave foránea al jugador ganador (puede ser nulo si la partida no ha terminado o se canceló)
    // Un jugador puede ganar muchas partidas Player 1 -> N Match
    public int? WinnerId {get; set;}
    public Player? Winner {get; set;}
    
    // Propiedad de navegación a la traza/histotria de logs de la partida
    public ICollection<GameLog> GameLogs {get; set;} = new List<GameLog>();
}