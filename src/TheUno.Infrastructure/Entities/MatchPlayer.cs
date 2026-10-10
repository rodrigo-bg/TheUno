namespace TheUno.Infrastructure.Entities;

public class MatchPlayer
{
    // Atributos para la clase 'MatchPlayer'
    public Guid MatchId {get; set;}
    public Match Match { get; set; } = null!;
    
    public Guid PlayerId {get; set;}
    public Player Player { get; set; } = null!;
    
    // Puesto en el que quedo al final de la partida
    public int Position { get; set; }
}