using System;
using TheUno.Core.Models;

namespace TheUno.Core.Events;

public class GameOverEventArgs : EventArgs
{
    public Player Winner { get; }
    public TimeSpan Duration { get; }
    public int TotalTurns { get; }
    public int WinnerScore { get; }

    public GameOverEventArgs(Player winner, TimeSpan duration, int totalTurns, int winnerScore)
    {
        Winner = winner;
        Duration = duration;
        TotalTurns = totalTurns;
        WinnerScore = winnerScore;
    }
}