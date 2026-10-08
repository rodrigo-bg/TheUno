using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TheUno.Core.Contracts;
using TheUno.Core.Events;
using TheUno.Core.Models;

namespace TheUno.Core.Engine;

public class GameEngine : IGameEngine
{
    private readonly Deck _deck;
    private readonly GameRules _rules;
    private readonly IGameLogRepository? _logRepository;
    
    private readonly List<Player> _players;
    private int _currentPlayerIndex;
    private int _direction; // 1 = horario, -1 = antihorario
    private CardColor _activeColor;
        
    private bool _hasDrawnThisTurn;
    private Card? _drawnCardThisTurn;
    private Player? _lastActingPlayer;
    
    private bool _isGameOver;
    private Guid _matchId;
    private DateTime _gameStartTime;
    private int _totalTurns;
    
    public IReadOnlyList<Player> Players
    {
        get
        {
            return _players;
        }
    }

    public Player CurrentPlayer
    {
        get
        {
            if (_players.Count == 0)
            {
                throw new InvalidOperationException("La partida no ha sido iniciada.");
            }
            return _players[_currentPlayerIndex];
        }
    }

    public Card TopCard
    {
        get
        {
            Card? top = _deck.GetTopCard();
            if (top == null)
            {
                throw new InvalidOperationException("No hay cartas en la mesa.");
            }
            return top;
        }
    }

    public CardColor ActiveColor
    {
        get
        {
            return _activeColor;
        }
    }

    public int Direction
    {
        get
        {
            return _direction;
        }
    }
    
    public event Action<CardPlayedEventArgs>? OnCardPlayed;
    public event Action<CardDrawnEventArgs>? OnCardDrawn;
    public event Action<TurnChangedEventArgs>? OnTurnChanged;
    public event Action<GameOverEventArgs>? OnGameOver;
    public event Action<Player>? OnUnoPenalty;

    public GameEngine(IGameLogRepository? logRepository = null)
    {
        _deck = new Deck();
        _rules = new GameRules();
        _logRepository = logRepository;
        _players = new List<Player>();
        _direction = 1;
        _activeColor = CardColor.None;
    }


// Configura la partida inicial
    public void StartGame(IEnumerable<string> playerNames)
    {
        if (playerNames == null)
        {
            throw new ArgumentNullException(nameof(playerNames));
        }

        _players.Clear();
        foreach (string name in playerNames)
        {
            _players.Add(new Player(name));
        }

        if (_players.Count != 3)
        {
            throw new InvalidOperationException("La partida de UNO requiere exactamente 3 jugadores.");
        }

        _deck.Reset();
        _direction = 1;
        _currentPlayerIndex = 0;
        _totalTurns = 1;
        _isGameOver = false;
        _hasDrawnThisTurn = false;
        _drawnCardThisTurn = null;
        _lastActingPlayer = null;
        _gameStartTime = DateTime.UtcNow;
        _matchId = Guid.NewGuid();
        
        for (int p = 0; p < _players.Count; p++)
        {
            for (int c = 0; c < 7; c++)
            {
                Card? card = _deck.Draw();
                if (card != null)
                {
                    _players[p].AddCard(card);
                }
            }
        }
        
        // 2. Extraer la primera carta inicial del descarte
        Card firstCard = _deck.SetupFirstDiscardCard();
        if (firstCard.Color == CardColor.None)
        {
            _activeColor = CardColor.Red; // Si la primera carta es un Wild, se inicia en Rojo por defecto
        }
        else
        {
            _activeColor = firstCard.Color;
        }
        
        // 3. Reglas oficiales de Mattel para la carta inicial volteada
        if (firstCard.Type == CardType.Skip)
        {
            // El jugador 1 pierde su turno
            AdvanceTurn(1);
            return;
        }

        if (firstCard.Type == CardType.Reverse)
        {
            // Invierte la dirección de inicio (antihorario)
            _direction = -1;
        }
        else if (firstCard.Type == CardType.DrawTwo)
        {
            // El primer jugador roba 2 cartas y es saltado
            Player firstPlayer = _players[0];
            GiveCardsToPlayer(firstPlayer, 2);
            AdvanceTurn(1);
            return;
        }

        NotifyTurnChanged();
    }

    
}