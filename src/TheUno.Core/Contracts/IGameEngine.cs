using TheUno.Core.Events;
using TheUno.Core.Models;

namespace TheUno.Core.Contracts;

public interface IGameEngine
{
    // Estado visible para la UI
    IReadOnlyList<Player> Players { get; }
    Player CurrentPlayer { get; }
    Card TopCard { get; }
    CardColor ActiveColor { get; }
    int Direction { get; } // 1: Horario, -1: Antihorario

    // Eventos a los que se suscribe la UI y el Logger
    event Action<CardPlayedEventArgs>? OnCardPlayed;
    event Action<CardDrawnEventArgs>? OnCardDrawn;
    event Action<TurnChangedEventArgs>? OnTurnChanged;
    event Action<GameOverEventArgs>? OnGameOver;
    event Action<Player>? OnUnoPenalty;

    // Métodos que invocará la UI
    void StartGame(IEnumerable<string> playerNames);
    bool CanPlayCard(Card card);
    void PlayCard(Card card, CardColor? chosenColor = null);
    void DrawCard();
    void CallUno();
}

