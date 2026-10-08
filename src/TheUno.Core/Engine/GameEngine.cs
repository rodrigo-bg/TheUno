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
    
    // consulta si una carta puede ser jugada o no

    public bool CanPlayCard(Card card)
    {
        if (_isGameOver || card == null || _players.Count == 0)
        {
            return false;
        }
        
        // Si ya robó este turno, las reglas de Mattel solo permiten tirar la carta recién robada
        if (_hasDrawnThisTurn)
        {
            if (_drawnCardThisTurn == null || card.Id != _drawnCardThisTurn.Id)
            {
                return false;
            }
        }
        
        if (!CurrentPlayer.HasCard(card))
        {
            return false;
        }

        return _rules.IsPlayLegal(card, TopCard, _activeColor, CurrentPlayer.Hand);
    }
    
    // Ejecuta la jugada de una carta
    public void PlayCard(Card card, CardColor? chosenColor = null)
    {
        if (_isGameOver)
        {
            throw new InvalidOperationException("La partida ya ha finalizado.");
        }

        if (!CanPlayCard(card))
        {
            throw new InvalidOperationException("La carta seleccionada no es una jugada legal.");
        }

        // Validación de comodines
        if (card.Type == CardType.Wild || card.Type == CardType.WildDrawFour)
        {
            if (!chosenColor.HasValue || chosenColor.Value == CardColor.None)
            {
                throw new ArgumentException("Debes elegir un color válido al jugar un comodín.");
            }
        }

        // Penalización automática de UNO al jugador anterior si omitió cantar UNO
        CheckUnoPenaltyForPreviousPlayer();

        Player actingPlayer = CurrentPlayer;

        // Mover carta de la mano al descarte
        actingPlayer.RemoveCard(card);
        _deck.Discard(card);

        // Actualizar color activo
        if (card.Type == CardType.Wild || card.Type == CardType.WildDrawFour)
        {
            _activeColor = chosenColor.Value;
        }
        else
        {
            _activeColor = card.Color;
        }

        _hasDrawnThisTurn = false;
        _drawnCardThisTurn = null;
        _lastActingPlayer = actingPlayer;

        // Disparar evento de carta jugada
        if (OnCardPlayed != null)
        {
            OnCardPlayed(new CardPlayedEventArgs(actingPlayer, card, _activeColor));
        }

        LogAction(actingPlayer, GameActionType.PlayCard, card.GetDisplayText(), chosenColor?.ToString());

        // Comprobación de fin de partida (Mano vacía)
        if (actingPlayer.Hand.Count == 0)
        {
            EndGame(actingPlayer);
            return;
        }

        // Si le queda más de 1 carta, se reinicia la bandera de UNO
        if (actingPlayer.Hand.Count > 1)
        {
            actingPlayer.HasDeclaredUno = false;
        }

        // Procesar efectos especiales de la carta tirada
        ProcessCardEffects(card);
    }
    
    //Permite al jugador robar una carta del maso si no quiere o no puede jugar
    
    public void DrawCard()
    {
        if (_isGameOver)
        {
            throw new InvalidOperationException("La partida ya ha finalizado.");
        }

        if (_hasDrawnThisTurn)
        {
            throw new InvalidOperationException("Ya has robado una carta en este turno.");
        }

        CheckUnoPenaltyForPreviousPlayer();

        Player actingPlayer = CurrentPlayer;
        Card? drawnCard = _deck.Draw();

        if (drawnCard == null)
        {
            // Mazo y descarte totalmente vacíos: pasa el turno
            AdvanceTurn(1);
            return;
        }

        actingPlayer.AddCard(drawnCard);
        actingPlayer.HasDeclaredUno = false;

        if (OnCardDrawn != null)
        {
            OnCardDrawn(new CardDrawnEventArgs(actingPlayer, drawnCard));
        }

        LogAction(actingPlayer, GameActionType.DrawCard, drawnCard.GetDisplayText(), null);

        // Verificar si la carta robada se puede tirar de inmediato
        bool canPlayDrawn = _rules.IsPlayLegal(drawnCard, TopCard, _activeColor, actingPlayer.Hand);

        if (canPlayDrawn)
        {
            // Se le permite jugarla o pasar
            _hasDrawnThisTurn = true;
            _drawnCardThisTurn = drawnCard;
        }
        else
        {
            // Si no se puede jugar, pasa el turno automáticamente según Mattel
            AdvanceTurn(1);
        }
    }
    
    //Pasa el turno si el jugador ya robo y decidio no jugar la carta
    public void PassTurn()
    {
        if (_isGameOver)
        {
            throw new InvalidOperationException("La partida ya ha finalizado.");
        }

        if (!_hasDrawnThisTurn)
        {
            throw new InvalidOperationException("No puedes pasar el turno sin haber robado primero.");
        }

        LogAction(CurrentPlayer, GameActionType.PassTurn, "Paso de turno", null);
        AdvanceTurn(1);
    }
    

    // Declara el estado de 'UNO' para el jugador en turno.
    public void CallUno()
    {
        if (_isGameOver)
        {
            return;
        }

        // Legal si tiene 1 carta o si tiene 2 y está por tirar una
        if (CurrentPlayer.Hand.Count <= 2)
        {
            CurrentPlayer.HasDeclaredUno = true;
            LogAction(CurrentPlayer, GameActionType.CallUno, "Declaro UNO", null);
        }
    }
    
        // METODOS INTERNOS PARA EL CONTROL DE FLUJO DEL PROGRAMA
        
        private void ProcessCardEffects(Card card)
        {
            switch (card.Type)
            {
                case CardType.Reverse:
                    // En 3 jugadores invierte formalmente el sentido del juego
                    _direction = _direction * -1;
                    AdvanceTurn(1);
                    break;

                case CardType.Skip:
                    // Salta al siguiente jugador (avanza 2 posiciones)
                    AdvanceTurn(2);
                    break;

                case CardType.DrawTwo:
                    // El siguiente jugador roba 2 y pierde su turno
                    Player victimDrawTwo = GetPlayerAtOffset(1);
                    GiveCardsToPlayer(victimDrawTwo, 2);
                    AdvanceTurn(2);
                    break;

                case CardType.WildDrawFour:
                    // El siguiente jugador roba 4 y pierde su turno
                    Player victimDrawFour = GetPlayerAtOffset(1);
                    GiveCardsToPlayer(victimDrawFour, 4);
                    AdvanceTurn(2);
                    break;

                default:
                    // Cartas numéricas o Wild simple
                    AdvanceTurn(1);
                    break;
            }
        }

        
        /// Calcula el índice cíclico en el anillo de 3 jugadores usando aritmética modular.
        
        private int CalculateIndex(int steps)
        {
            int count = _players.Count;
            int nextIndex = (_currentPlayerIndex + (steps * _direction)) % count;
            if (nextIndex < 0)
            {
                nextIndex = nextIndex + count;
            }
            return nextIndex;
        }

        private Player GetPlayerAtOffset(int steps)
        {
            int targetIndex = CalculateIndex(steps);
            return _players[targetIndex];
        }

        private void AdvanceTurn(int steps)
        {
            _currentPlayerIndex = CalculateIndex(steps);
            _totalTurns++;
            _hasDrawnThisTurn = false;
            _drawnCardThisTurn = null;

            NotifyTurnChanged();
        }
        
        private void NotifyTurnChanged()
        {
            if (OnTurnChanged != null)
            {
                OnTurnChanged(new TurnChangedEventArgs(CurrentPlayer, _direction, TopCard, _activeColor));
            }
        }
        
        
}