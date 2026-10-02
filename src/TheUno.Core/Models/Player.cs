using System;
using System.Collections.Generic;

namespace TheUno.Core.Models;

public class Player {
    public Guid Id { get; }
    public string Name { get; }
    public List<Card> Hand { get; }
    public bool HasDeclaredUno { get; set; }

    public Player(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
        Hand = new List<Card>();
        HasDeclaredUno = false;
    }
}