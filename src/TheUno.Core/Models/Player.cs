namespace TheUno.Core.Models;

public class Player
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public List<Card> Hand { get; } = new();

    public Player(string name)
    {
        Name = name;
    }   
}