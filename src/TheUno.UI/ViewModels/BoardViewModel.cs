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
using TheUno.UI.Services;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Estado visual de la mesa. La partida la lleva el motor de juego de TheUno.Core:
/// este ViewModel solo refleja su estado y escucha sus eventos.
/// </summary>
public partial class BoardViewModel : ViewModelBase
{
    // Tiempo para cantar UNO después de tirar la penúltima carta
    private static readonly TimeSpan UnoWindow = TimeSpan.FromSeconds(2);

    // Tiempo que se ve el aviso de penalización por no cantar UNO
    private static readonly TimeSpan PenaltyNoticeDuration = TimeSpan.FromSeconds(2.5);

    // Motor de juego: lo crea MainWindowViewModel y se recibe en el constructor
    private readonly IGameEngine _engine;

    // Navegación: las pone MainWindowViewModel
    private readonly Action _goToMenu;
    private readonly Action _startNewGame;

    // Avisa que un jugador cantó UNO, con el número de turno, para guardarlo en el log
    private readonly Action<Player, int>? _onUnoCalled;

    // Número de turno igual al que usa el motor en su log: empieza en 1 y sube en cada cambio de turno
    private int _turnNumber;

    // Turno en que el jugador de la ventana de UNO tiró su penúltima carta
    private int _unoTurnNumber;

    // Qué hacer con el color elegido (tirar el comodín con ese color).
    private Action<CardColor>? _onColorChosen;

    private bool _isGameOver;

    // Ventana para cantar UNO: quién puede cantar, cuándo se acaba y el cambio de turno que espera.
    private PlayerViewModel? _unoPlayer;
    private DateTime _unoDeadline;
    private DispatcherTimer? _unoTimer;
    private TurnChangedEventArgs? _pendingTurnChange;

    // Cuenta los avisos de penalización: si sale otro antes de que se oculte el primero,
    // el temporizador del primero ya no lo oculta.
    private int _penaltyNoticeCount;

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

    /// <summary>Tiempo que queda para cantar UNO, de 1 (recién empieza) a 0 (se acabó). Llena la barra del botón.</summary>
    [ObservableProperty]
    private double _unoTimeLeft;

    // ---------- Diálogos ----------

    /// <summary>Muestra el selector de color que aparece al tirar un comodín o un +4.</summary>
    [ObservableProperty]
    private bool _isColorPickerOpen;

    /// <summary>Muestra el aviso de que un jugador no cantó UNO y recibió 2 cartas.</summary>
    [ObservableProperty]
    private bool _isPenaltyNoticeOpen;

    /// <summary>Texto del aviso, por ejemplo "¡JUGADOR 2 NO CANTÓ UNO! +2".</summary>
    [ObservableProperty]
    private string _penaltyMessage = string.Empty;

    /// <summary>Muestra la alerta que confirma si se quiere abandonar la partida.</summary>
    [ObservableProperty]
    private bool _isExitDialogOpen;

    /// <summary>Muestra la pantalla de victoria (aparece 3 segundos después de que alguien gana).</summary>
    [ObservableProperty]
    private bool _isVictoryScreenOpen;

    /// <summary>"¡¡GANASTE!!" si ganó el jugador de esta compu (el de abajo) o "¡¡PERDISTE!!" si ganó otro.</summary>
    [ObservableProperty]
    private string _victoryTitle = string.Empty;

    /// <summary>
    /// Nombre del ganador en mayúsculas, para la pantalla de victoria. Si ganó otro dice "Ganó RODRIGO"
    /// (en minúsculas porque la fuente pixel no tiene la "Ó" mayúscula).
    /// </summary>
    [ObservableProperty]
    private string _winnerName = string.Empty;

    /// <summary>Duración de la partida para la pantalla de victoria, en minutos y segundos (ej. "5:32").</summary>
    [ObservableProperty]
    private string _gameDurationText = string.Empty;

    /// <summary>
    /// Recibe el motor ya iniciado (con las cartas repartidas y la primera carta volteada).
    /// </summary>
    public BoardViewModel(IGameEngine engine, Action goToMenu, Action startNewGame,
        Action<Player, int>? onUnoCalled = null)
    {
        _engine = engine;
        _goToMenu = goToMenu;
        _startNewGame = startNewGame;
        _onUnoCalled = onUnoCalled;

        // Si la primera carta es Salto o +2, el motor ya avanzó un turno al iniciar
        _turnNumber = _engine.TopCard.Type is CardType.Skip or CardType.DrawTwo ? 2 : 1;

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

        // Se escucha al final porque los PlayerViewModel tienen que existir antes.
        _engine.OnTurnChanged += OnTurnChanged;
        _engine.OnCardPlayed += OnCardPlayed;
        _engine.OnCardDrawn += OnCardDrawn;
        _engine.OnGameOver += OnGameOver;
        _engine.OnUnoPenalty += OnUnoPenalty;
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public BoardViewModel() : this(CreateDesignEngine(), () => { }, () => { })
    {
    }

    private static IGameEngine CreateDesignEngine()
    {
        var engine = new GameEngine();
        engine.StartGame(new[] { "Jugador 1", "Jugador 2", "Jugador 3" });
        return engine;
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

    /// <summary>
    /// El motor cambió de turno. Si el jugador anterior tiene abierta su ventana para cantar UNO,
    /// el cambio se guarda y se muestra hasta que cante o se acabe el tiempo.
    /// </summary>
    private void OnTurnChanged(TurnChangedEventArgs e)
    {
        _turnNumber++;

        if (_unoPlayer is not null)
        {
            _pendingTurnChange = e;
            // El motor ya avanzó: ninguna carta queda jugable hasta mostrar el nuevo turno.
            UpdatePlayableCards();
            return;
        }

        ApplyTurnChange(e);
    }

    /// <summary>Refresca toda la mesa con el estado del motor después de un cambio de turno.</summary>
    private void ApplyTurnChange(TurnChangedEventArgs e)
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
    /// Si era la penúltima, se abre su ventana para cantar UNO.
    /// </summary>
    private void OnCardPlayed(CardPlayedEventArgs e)
    {
        App.Sounds.Play(SoundEffects.CardSound);

        var player = GetPlayerViewModel(e.Player);
        player.SyncHand();
        TopDiscardCard = new CardViewModel(e.Card);
        ActiveColor = e.ActiveColor;
        UpdatePlayableCards();

        if (e.Player.Hand.Count == 1 && !e.Player.HasDeclaredUno)
        {
            // El cambio de turno todavía no llega: este es el turno en que tiró la carta
            _unoTurnNumber = _turnNumber;
            OpenUnoWindow(player);
        }
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

    private bool CanDrawCard() => !CanPassTurn && !_isGameOver && _unoPlayer is null;

    [RelayCommand(CanExecute = nameof(CanPassTurn))]
    private void PassTurn() => _engine.PassTurn();

    /// <summary>
    /// Aparece la carta robada en la mano de quien la recibió (también las de un +2 o +4,
    /// o las de la penalización por no cantar UNO).
    /// </summary>
    private void OnCardDrawn(CardDrawnEventArgs e)
    {
        App.Sounds.Play(SoundEffects.CardSound);
        GetPlayerViewModel(e.Player).SyncHand();
    }

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

    /// <summary>
    /// Después de tirar su penúltima carta, el jugador tiene 2 segundos para picar ¡UNO!.
    /// Mientras tanto se ve una barra que se vacía y el turno del siguiente todavía no se muestra.
    /// </summary>
    private void OpenUnoWindow(PlayerViewModel player)
    {
        _unoPlayer = player;
        _unoDeadline = DateTime.UtcNow + UnoWindow;
        UnoTimeLeft = 1;
        player.ShowUnoButton = true;
        DrawCardCommand.NotifyCanExecuteChanged();

        _unoTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Normal, OnUnoTimerTick);
        _unoTimer.Start();
    }

    private void OnUnoTimerTick(object? sender, EventArgs e)
    {
        var left = (_unoDeadline - DateTime.UtcNow) / UnoWindow;
        if (left <= 0)
        {
            CloseUnoWindow();
            return;
        }

        UnoTimeLeft = left;
    }

    /// <summary>Se acabó el tiempo o ya cantó: se quita el botón y se muestra el turno del siguiente.</summary>
    private void CloseUnoWindow()
    {
        _unoTimer?.Stop();
        _unoTimer = null;
        UnoTimeLeft = 0;

        if (_unoPlayer is not null) _unoPlayer.ShowUnoButton = false;
        _unoPlayer = null;
        DrawCardCommand.NotifyCanExecuteChanged();

        if (_pendingTurnChange is { } pending)
        {
            _pendingTurnChange = null;
            ApplyTurnChange(pending);
        }
    }

    /// <summary>El jugador dueño del botón ¡UNO! canta UNO: el turno pasa en ese momento.</summary>
    [RelayCommand]
    private void CallUno(PlayerViewModel player)
    {
        player.HasCalledUno = true;

        // Cuando aparece el botón el motor ya le pasó el turno al siguiente, así que no sirve
        // CallUno() del motor (solo marca al jugador en turno). Se marca directo al jugador
        // del Core: el motor revisa esta propiedad antes de castigar con +2.
        player.Model.HasDeclaredUno = true;

        // Por lo mismo, el motor no registra el UNO en el log: se avisa para guardarlo aparte
        _onUnoCalled?.Invoke(player.Model, _unoTurnNumber);

        if (player == _unoPlayer) CloseUnoWindow();
    }

    /// <summary>
    /// El motor castigó a un jugador con 2 cartas por no cantar UNO. Las cartas ya llegaron
    /// a su mano (OnCardDrawn); aquí solo se muestra el aviso unos segundos.
    /// </summary>
    private void OnUnoPenalty(Player player)
    {
        PenaltyMessage = $"¡{player.Name.ToUpperInvariant()} NO CANTÓ UNO! +2";
        IsPenaltyNoticeOpen = true;

        var notice = ++_penaltyNoticeCount;
        DispatcherTimer.RunOnce(() =>
        {
            if (notice == _penaltyNoticeCount) IsPenaltyNoticeOpen = false;
        }, PenaltyNoticeDuration);
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

    /// <summary>El motor avisa que alguien se quedó sin cartas, con la duración de la partida.</summary>
    private void OnGameOver(GameOverEventArgs e)
    {
        GameDurationText = $"{(int)e.Duration.TotalMinutes}:{e.Duration.Seconds:00}";
        ShowVictory(GetPlayerViewModel(e.Winner));
    }

    /// <summary>
    /// Termina la partida: tras una pausa de 1.5 segundos (para que se vea la última carta en la mesa)
    /// aparece la pantalla final con el nombre del ganador y suena victoria o derrota.
    /// La música se pausa mientras suena y sigue al terminar.
    /// Mientras tanto ya no se puede robar ni tirar.
    /// </summary>
    public void ShowVictory(PlayerViewModel winner)
    {
        if (_isGameOver) return;
        _isGameOver = true;

        CanPassTurn = false;
        DrawCardCommand.NotifyCanExecuteChanged();
        UpdatePlayableCards();

        // El jugador de esta compu siempre está abajo (Player1)
        var iWon = winner == Player1;

        DispatcherTimer.RunOnce(() =>
        {
            var name = winner.Name.ToUpperInvariant();
            VictoryTitle = iWon ? "¡¡GANASTE!!" : "¡¡PERDISTE!!";
            WinnerName = iWon ? name : $"Ganó {name}";
            IsVictoryScreenOpen = true;

            App.Music.Pause();
            App.Sounds.Play(iWon ? SoundEffects.WinSound : SoundEffects.LoseSound, App.Music.Resume);
        }, TimeSpan.FromSeconds(1.5));
    }

    [RelayCommand]
    private void BackToMenu() => _goToMenu();

    [RelayCommand]
    private void PlayAgain() => _startNewGame();
}
