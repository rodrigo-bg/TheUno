using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheUno.Core.Contracts;
using TheUno.Core.Engine;
using TheUno.Core.Events;
using TheUno.Core.Models;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Estado visual de la mesa. La partida la lleva el motor de juego de TheUno.Core:
/// este ViewModel solo refleja su estado y escucha sus eventos.
/// </summary>
public partial class BoardViewModel : ViewModelBase
{
    private static readonly string[] PlayerNames = { "Jugador 1", "Jugador 2", "Jugador 3" };

    private readonly IGameEngine _engine = new GameEngine();

    // Navegación: las pone MainWindowViewModel
    private readonly Action _goToMenu;
    private readonly Action _startNewGame;

    // Qué hacer con el color elegido (tirar el comodín con ese color).
    private Action<CardColor>? _onColorChosen;

    private bool _isGameOver;

    // ---------- Estado de la mesa ----------

    /// <summary>Jugador en turno. Al cambiar, se actualiza qué zona se resalta y qué manos se atenúan.</summary>
    [ObservableProperty]
    private PlayerViewModel _activePlayer;

    /// <summary>Color que se debe seguir jugando (el de la carta de arriba o el elegido con un comodín).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveColorBrush))]
    private CardColor _activeColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCounterClockwise))]
    [NotifyPropertyChangedFor(nameof(DirectionText))]
    private bool _isClockwise = true;

    /// <summary>Carta visible en la pila de descarte.</summary>
    [ObservableProperty]
    private CardViewModel _topDiscardCard;

    /// <summary>
    /// El jugador en turno ya robó y la carta robada se puede tirar: puede tirarla o pasar.
    /// Mientras tanto no puede volver a robar.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawCardCommand))]
    [NotifyCanExecuteChangedFor(nameof(PassTurnCommand))]
    private bool _canPassTurn;

    // ---------- Diálogos ----------

    /// <summary>Muestra el selector de color que aparece al tirar un comodín o un +4.</summary>
    [ObservableProperty]
    private bool _isColorPickerOpen;

    /// <summary>Muestra la alerta que confirma si se quiere abandonar la partida.</summary>
    [ObservableProperty]
    private bool _isExitDialogOpen;

    /// <summary>Muestra la pantalla de victoria (aparece 3 segundos después de que alguien gana).</summary>
    [ObservableProperty]
    private bool _isVictoryScreenOpen;

    /// <summary>Nombre del ganador en mayúsculas, para la pantalla de victoria.</summary>
    [ObservableProperty]
    private string _winnerName = string.Empty;

    public BoardViewModel(Action goToMenu, Action startNewGame)
    {
        _goToMenu = goToMenu;
        _startNewGame = startNewGame;

        // El motor reparte 7 cartas a cada jugador y voltea la primera carta del descarte.
        _engine.StartGame(PlayerNames);

        Player1 = new PlayerViewModel(_engine.Players[0]);
        Player2 = new PlayerViewModel(_engine.Players[1]);
        Player3 = new PlayerViewModel(_engine.Players[2]);

        // Estado inicial de la mesa. Puede no empezar el Jugador 1: si la primera carta
        // es Salto o +2, el motor ya le pasó el turno al siguiente.
        _topDiscardCard = new CardViewModel(_engine.TopCard);
        _activeColor = _engine.ActiveColor;
        _isClockwise = _engine.Direction == 1;
        _activePlayer = GetPlayerViewModel(_engine.CurrentPlayer);
        _activePlayer.IsActive = true;
        UpdatePlayableCards();

        // Se escucha después de StartGame porque los PlayerViewModel aún no existían.
        _engine.OnTurnChanged += OnTurnChanged;
        _engine.OnCardPlayed += OnCardPlayed;
        _engine.OnCardDrawn += OnCardDrawn;
        _engine.OnGameOver += OnGameOver;
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public BoardViewModel() : this(() => { }, () => { })
    {
    }

    public PlayerViewModel Player1 { get; }
    public PlayerViewModel Player2 { get; }
    public PlayerViewModel Player3 { get; }

    public Bitmap DrawPileImage => CardViewModel.BackImage;

    public bool IsCounterClockwise => !IsClockwise;

    public string DirectionText => IsClockwise ? "Horario" : "Antihorario";

    // Paleta tomada directamente de las cartas 8 bits
    public IBrush ActiveColorBrush => ActiveColor switch
    {
        CardColor.Red => new SolidColorBrush(Color.Parse("#FF0000")),
        CardColor.Green => new SolidColorBrush(Color.Parse("#40FF00")),
        CardColor.Blue => new SolidColorBrush(Color.Parse("#0020FF")),
        CardColor.Yellow => new SolidColorBrush(Color.Parse("#E6B400")),
        _ => new SolidColorBrush(Color.Parse("#555555"))
    };

    // ---------- Turno ----------

    partial void OnActivePlayerChanged(PlayerViewModel? oldValue, PlayerViewModel newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        newValue.IsActive = true;
    }

    /// <summary>El motor cambió de turno: se refresca toda la mesa con su estado.</summary>
    private void OnTurnChanged(TurnChangedEventArgs e)
    {
        Player1.SyncHand();
        Player2.SyncHand();
        Player3.SyncHand();

        if (TopDiscardCard.Card != e.TopCard) TopDiscardCard = new CardViewModel(e.TopCard);
        ActiveColor = e.ActiveColor;
        IsClockwise = e.Direction == 1;
        ActivePlayer = GetPlayerViewModel(e.CurrentPlayer);
        CanPassTurn = false;
        UpdatePlayableCards();
    }

    private PlayerViewModel GetPlayerViewModel(Player player)
    {
        if (player == Player1.Model) return Player1;
        if (player == Player2.Model) return Player2;
        return Player3;
    }

    /// <summary>
    /// Marca las cartas que el motor permite tirar. Solo pueden ser del jugador en turno;
    /// después de robar, solo la carta robada.
    /// </summary>
    private void UpdatePlayableCards()
    {
        foreach (var player in new[] { Player1, Player2, Player3 })
        {
            foreach (var card in player.Hand)
            {
                card.IsPlayable = _engine.CanPlayCard(card.Card);
            }
        }
    }

    // ---------- Tirar carta ----------

    /// <summary>
    /// El jugador en turno tira una carta. Si el motor no la permite, no pasa nada.
    /// Un comodín o +4 primero abre el selector de color, porque el motor pide el color al tirarlo.
    /// </summary>
    [RelayCommand]
    private void PlayCard(CardViewModel card)
    {
        if (!_engine.CanPlayCard(card.Card)) return;

        if (card.Card.Type is CardType.Wild or CardType.WildDrawFour)
        {
            AskForColor(color => _engine.PlayCard(card.Card, color));
            return;
        }

        _engine.PlayCard(card.Card);
    }

    /// <summary>
    /// Se refleja la carta tirada en la mesa. Normalmente después llega el cambio de turno,
    /// pero si era la última carta del jugador la partida termina y ya no hay cambio de turno.
    /// </summary>
    private void OnCardPlayed(CardPlayedEventArgs e)
    {
        GetPlayerViewModel(e.Player).SyncHand();
        TopDiscardCard = new CardViewModel(e.Card);
        ActiveColor = e.ActiveColor;
        UpdatePlayableCards();
    }

    // ---------- Robar y pasar ----------

    /// <summary>
    /// El jugador en turno roba una carta del mazo. Si no se puede tirar, el motor pasa el turno solo;
    /// si se puede, el jugador sigue en turno y elige entre tirarla o pasar.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDrawCard))]
    private void DrawCard()
    {
        var drawingPlayer = _engine.CurrentPlayer;
        _engine.DrawCard();
        CanPassTurn = _engine.CurrentPlayer == drawingPlayer;
        UpdatePlayableCards();
    }

    private bool CanDrawCard() => !CanPassTurn && !_isGameOver;

    [RelayCommand(CanExecute = nameof(CanPassTurn))]
    private void PassTurn() => _engine.PassTurn();

    /// <summary>
    /// Aparece la carta robada en la mano de quien la recibió (también las de un +2 o +4,
    /// o las de la penalización por no cantar UNO).
    /// </summary>
    private void OnCardDrawn(CardDrawnEventArgs e) => GetPlayerViewModel(e.Player).SyncHand();

    // ---------- Selector de color ----------

    /// <summary>
    /// Abre el selector de color. El juego no continúa hasta elegir uno: no hay opción de cancelar.
    /// </summary>
    public void AskForColor(Action<CardColor>? onChosen = null)
    {
        _onColorChosen = onChosen;
        IsColorPickerOpen = true;
    }

    [RelayCommand]
    private void ChooseColor(CardColor color)
    {
        IsColorPickerOpen = false;
        ActiveColor = color;

        var onChosen = _onColorChosen;
        _onColorChosen = null;
        onChosen?.Invoke(color);
    }

    // ---------- UNO ----------

    /// <summary>El jugador dueño del botón ¡UNO! canta UNO.</summary>
    [RelayCommand]
    private void CallUno(PlayerViewModel player)
    {
        player.HasCalledUno = true;

        // El botón aparece después de tirar la penúltima carta, cuando el turno ya es de otro,
        // así que no sirve CallUno() del motor (solo marca al jugador en turno). Se marca
        // directo al jugador del Core: el motor revisa esta propiedad antes de castigar con +2.
        player.Model.HasDeclaredUno = true;
    }

    // ---------- Salir de la partida ----------

    [RelayCommand]
    private void RequestExit() => IsExitDialogOpen = true;

    [RelayCommand]
    private void CancelExit() => IsExitDialogOpen = false;

    [RelayCommand]
    private void ConfirmExit()
    {
        IsExitDialogOpen = false;
        _goToMenu();
    }

    // ---------- Victoria ----------

    /// <summary>El motor avisa que alguien se quedó sin cartas.</summary>
    private void OnGameOver(GameOverEventArgs e) => ShowVictory(GetPlayerViewModel(e.Winner));

    /// <summary>
    /// Termina la partida: tras una pausa de 3 segundos (para que se vea la última carta en la mesa)
    /// aparece la pantalla de victoria con el nombre del ganador.
    /// Mientras tanto ya no se puede robar ni tirar.
    /// </summary>
    public void ShowVictory(PlayerViewModel winner)
    {
        if (_isGameOver) return;
        _isGameOver = true;

        CanPassTurn = false;
        DrawCardCommand.NotifyCanExecuteChanged();
        UpdatePlayableCards();

        DispatcherTimer.RunOnce(() =>
        {
            WinnerName = winner.Name.ToUpperInvariant();
            IsVictoryScreenOpen = true;
        }, TimeSpan.FromSeconds(3));
    }

    [RelayCommand]
    private void BackToMenu() => _goToMenu();

    [RelayCommand]
    private void PlayAgain() => _startNewGame();
}
