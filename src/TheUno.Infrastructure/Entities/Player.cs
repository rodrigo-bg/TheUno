namespace TheUno.Infrastructure.Entities;

// Esta clase representa a los jugadores de la partida
public class Player
{
    // Atributos de la entidad 'Player'
    public Guid Id {get; set;} = Guid.NewGuid();
    public string Name {get; set;} = string.Empty;
    public DateTime CreatedAt {get; set;} = DateTime.UtcNow;
    
    // Propiedades de navegación
    public ICollection<Match> WonMatches {get; set;} = new List<Match>();
    public ICollection<GameLog> GameLogs {get; set;} = new List<GameLog>();
}