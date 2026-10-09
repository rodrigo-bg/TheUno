using System;
using System.Collections.Generic;

namespace TheUno.Core.Models;

public class Player {
    public Guid Id { get; } // clave primaria del jugador
    public string Name { get; }
    public List<Card> Hand { get; } // Coleccion de cartas del jugador
    public bool HasDeclaredUno { get; set; } 

    public Player(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
        Hand = new List<Card>();
        HasDeclaredUno = false;
    }
    
    public Player(Guid id, string name) // Constructor para jugadores ya registrados
    {
        Id = id;
        Name = name;
        Hand = new List<Card>();
        HasDeclaredUno = false;
    }
    
    public int CalculateHandScore() // Calcula la puntuacion final del jugador
    {
        int totalScore = 0;
        for (int i = 0; i < Hand.Count; i++)
        {
            totalScore += Hand[i].GetScoreValue();
        }
        return totalScore;
    }
    
    public void AddCard(Card card)
    {
        if (card != null)
        {
            Hand.Add(card);
        }
    }

    public bool RemoveCard(Card card)
    {
        if (card == null)
        {
            return false;
        }
        return Hand.Remove(card);
    }

    public bool HasCard(Card card)
    {
        if (card == null)
        {
            return false;
        }
        return Hand.Contains(card);
    }
}