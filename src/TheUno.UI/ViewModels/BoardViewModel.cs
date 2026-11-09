using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheUno.Core.Models;
using TheUno.UI.Services;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Estado visual de la mesa. La partida vive en el servidor: este ViewModel no conoce las reglas,
/// solo dibuja el estado que le mandan. Abajo siempre está el jugador de esta compu.
/// </summary>
public partial class BoardViewModel : ViewModelBase
{
    // Tiempo que se ve el aviso de penalización por no cantar UNO
    private static readonly TimeSpan PenaltyNoticeDuration = TimeSpan.FromSeconds(2.5);

    // La partida del servidor. Solo es null en el diseñador de Rider/Avalonia
    private readonly IGameSession? _session;

    // Navegación: las pone MainWindowViewModel
    private readonly Action _goToMenu;
    private readonly Action _startNewGame;

    // Qué hacer con el color elegido (tirar el comodín con ese color).
    private Action<CardColor>? _onColorChosen;

    private bool _isGameOver;

    // Ventana para cantar UNO: cuánto dura, cuándo se acaba y su temporizador. El turno lo detiene el servidor.
    private TimeSpan _unoWindow;
    private DateTime _unoDeadline;
    private DispatcherTimer? _unoTimer;

    // Lo último que se vio en la mesa, para saber cuándo se tiró o se robó una carta
    private Guid _lastTopCardId;
    private int _lastCardTotal;

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

    /// <summary>Es el turno de esta compu y todavía no ha robado.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawCardCommand))]
    private bool _canDraw;

    /// <summary>
    /// El jugador en turno ya robó y la carta robada se puede tirar: puede tirarla o pasar.
    /// Mientras tanto no puede volver a robar.
    /// </summary>
    [ObservableProperty]
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

    /// <summary>Muestra la pantalla de victoria (aparece poco después de que alguien gana).</summary>
    [ObservableProperty]
    private bool _isVictoryScreenOpen;

    /// <summary>"¡¡GANASTE!!" si ganó el jugador de esta compu (el de abajo) o "¡¡PERDISTE!!" si ganó otro.</summary>
    [ObservableProperty]
    private string _victoryTitle = string.Empty;

    /// <summary>
    /// Nombre del ganador en mayúsculas, para la pantalla de victoria. Si ganó otro dice "Ganó RODRI"
    /// (en minúsculas porque la fuente pixel no tiene la "Ó" mayúscula).
    /// </summary>
    [ObservableProperty]
    private string _winnerName = string.Empty;

    /// <summary>Duración de la partida para la pantalla de victoria, en minutos y segundos (ej. "5:32").</summary>
    [ObservableProperty]
    private string _gameDurationText = string.Empty;

    /// <summary>Muestra el aviso de que la partida se interrumpió y solo queda regresar al menú.</summary>
    [ObservableProperty]
    private bool _isInterruptedNoticeOpen;

    /// <summary>Por qué se interrumpió, por ejemplo "Rodri se salió de la partida".</summary>
    [ObservableProperty]
    private string _interruptedMessage = string.Empty;

    /// <summary>Recibe la partida del servidor con su primera mesa ya lista.</summary>
    public BoardViewModel(IGameSession session, Action goToMenu, Action startNewGame)
        : this(session, session.State ?? throw new ArgumentException("La partida todavía no tiene mesa.", nameof(session)),
            goToMenu, startNewGame)
    {
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public BoardViewModel() : this(null, CreateDesignState(), () => { }, () => { })
    {
    }

    private BoardViewModel(IGameSession? session, GameStateDto state, Action goToMenu, Action startNewGame)
    {
        _session = session;
        _goToMenu = goToMenu;
        _startNewGame = startNewGame;

        // Los jugadores llegan en el orden de los turnos: se gira la lista para que esta compu quede abajo
        var mySeat = state.Players.FindIndex(p => p.Name == state.MyName);
        var names = Enumerable.Range(0, state.Players.Count)
            .Select(i => state.Players[(mySeat + i) % state.Players.Count].Name)
            .ToList();
        Player1 = new PlayerViewModel(names[0]);
        Player2 = new PlayerViewModel(names[1]);
        Player3 = new PlayerViewModel(names[2]);

        _topDiscardCard = new CardViewModel(state.TopCard.ToCard());
        _activePlayer = GetPlayerViewModel(state.CurrentPlayerName);
        _activePlayer.IsActive = true;
        _lastTopCardId = state.TopCard.Id;
        _lastCardTotal = state.Players.Sum(p => p.CardCount);
        ApplyState(state);

        // Se escucha al final porque los PlayerViewModel tienen que existir antes.
        if (session is not null)
        {
            session.StateChanged += ApplyState;
            session.UnoWindowOpened += OnUnoWindowOpened;
            session.UnoPenalty += OnUnoPenalty;
            session.GameOver += OnGameOver;
            session.PlayerLeft += OnPlayerLeft;
            session.ConnectionLost += OnConnectionLost;
        }
    }

    private static GameStateDto CreateDesignState()
    {
        var hand = new List<CardDto>
        {
            new(Guid.NewGuid(), CardColor.Red, CardType.Number, 7),
            new(Guid.NewGuid(), CardColor.Blue, CardType.Skip, null),
            new(Guid.NewGuid(), CardColor.None, CardType.Wild, null)
        };
        var players = new List<PlayerStateDto>
        {
            new("Jugador 1", hand.Count, false),
            new("Jugador 2", 7, false),
            new("Jugador 3", 7, false)
        };

        return new GameStateDto("Jugador 1", hand, new List<Guid>(), players, "Jugador 1",
            new CardDto(Guid.NewGuid(), CardColor.Red, CardType.Number, 3), CardColor.Red, 1, true, false);
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

    /// <summary>Dibuja la mesa tal como la mandó el servidor.</summary>
    private void ApplyState(GameStateDto state)
    {
        PlayCardSoundIfNeeded(state);

        Player1.SyncHand(state.MyHand.Select(c => c.ToCard()).ToList(), state.PlayableCardIds);

        foreach (var player in state.Players)
        {
            var viewModel = GetPlayerViewModel(player.Name);
            viewModel.CardCount = player.CardCount;
            viewModel.HasCalledUno = player.HasCalledUno && player.CardCount == 1;
        }

        if (TopDiscardCard.Card.Id != state.TopCard.Id) TopDiscardCard = new CardViewModel(state.TopCard.ToCard());
        ActiveColor = state.ActiveColor;
        IsClockwise = state.Direction == 1;
        ActivePlayer = GetPlayerViewModel(state.CurrentPlayerName);
        CanDraw = state.CanDraw;
        CanPassTurn = state.CanPass;
    }

    /// <summary>Suena la carta si cambió la de arriba o alguien recibió cartas (robar, +2, +4, penalización).</summary>
    private void PlayCardSoundIfNeeded(GameStateDto state)
    {
        var cardTotal = state.Players.Sum(p => p.CardCount);
        if (state.TopCard.Id != _lastTopCardId || cardTotal > _lastCardTotal)
        {
            App.Sounds.Play(SoundEffects.CardSound);
        }

        _lastTopCardId = state.TopCard.Id;
        _lastCardTotal = cardTotal;
    }

    private PlayerViewModel GetPlayerViewModel(string name)
    {
        if (name == Player1.Name) return Player1;
        if (name == Player2.Name) return Player2;
        return Player3;
    }

    // ---------- Tirar carta ----------

    /// <summary>
    /// El jugador en turno tira una carta. Un comodín o +4 primero abre el selector de color,
    /// porque el color se manda junto con la carta. La mesa cambia cuando el servidor contesta.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task PlayCardAsync(CardViewModel card)
    {
        if (!card.IsPlayable) return;

        if (card.Card.Type is CardType.Wild or CardType.WildDrawFour)
        {
            AskForColor(color => _ = SendAsync(session => session.PlayCardAsync(card.Card.Id, color)));
            return;
        }

        await SendAsync(session => session.PlayCardAsync(card.Card.Id));
    }

    /// <summary>
    /// Manda una jugada al servidor. Si la rechaza (por ejemplo, un doble clic), la mesa se queda como está.
    /// </summary>
    private async Task SendAsync(Func<IGameSession, Task> move)
    {
        if (_session is null) return;

        try
        {
            await move(_session);
        }
        catch (Exception)
        {
            // Las reglas las decide el servidor: aquí no hay nada que deshacer
        }
    }

    // ---------- Robar y pasar ----------

    /// <summary>
    /// El jugador en turno roba una carta del mazo. Si no se puede tirar, el servidor pasa el turno solo;
    /// si se puede, el jugador sigue en turno y elige entre tirarla o pasar.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDrawCard))]
    private Task DrawCardAsync() => SendAsync(session => session.DrawCardAsync());

    private bool CanDrawCard() => CanDraw && !_isGameOver;

    [RelayCommand(CanExecute = nameof(CanPassTurn))]
    private Task PassTurnAsync() => SendAsync(session => session.PassTurnAsync());

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

        var onChosen = _onColorChosen;
        _onColorChosen = null;
        onChosen?.Invoke(color);
    }

    // ---------- UNO ----------

    /// <summary>
    /// Un jugador tiró su penúltima carta. Si fue el de esta compu, tiene unos segundos para picar ¡UNO!
    /// y se ve una barra que se vacía. El servidor lleva el tiempo y detiene el turno del siguiente.
    /// </summary>
    private void OnUnoWindowOpened(string playerName, int milliseconds)
    {
        if (playerName != Player1.Name) return;

        _unoWindow = TimeSpan.FromMilliseconds(milliseconds);
        _unoDeadline = DateTime.UtcNow + _unoWindow;
        UnoTimeLeft = 1;
        Player1.ShowUnoButton = true;

        _unoTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Normal, OnUnoTimerTick);
        _unoTimer.Start();
    }

    private void OnUnoTimerTick(object? sender, EventArgs e)
    {
        var left = (_unoDeadline - DateTime.UtcNow) / _unoWindow;
        if (left <= 0)
        {
            CloseUnoWindow();
            return;
        }

        UnoTimeLeft = left;
    }

    /// <summary>Se acabó el tiempo o ya cantó: se quita el botón.</summary>
    private void CloseUnoWindow()
    {
        _unoTimer?.Stop();
        _unoTimer = null;
        UnoTimeLeft = 0;
        Player1.ShowUnoButton = false;
    }

    /// <summary>El jugador de esta compu pica ¡UNO!.</summary>
    [RelayCommand]
    private async Task CallUnoAsync(PlayerViewModel player)
    {
        CloseUnoWindow();
        await SendAsync(session => session.CallUnoAsync());
    }

    /// <summary>
    /// Un jugador recibió 2 cartas por no cantar UNO. Las cartas llegan con el siguiente estado;
    /// aquí solo se muestra el aviso unos segundos.
    /// </summary>
    private void OnUnoPenalty(string playerName)
    {
        PenaltyMessage = $"¡{playerName.ToUpperInvariant()} NO CANTÓ UNO! +2";
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
        LeaveInBackground();
        _goToMenu();
    }

    private void OnPlayerLeft(string playerName)
    {
        if (playerName != Player1.Name) ShowInterrupted($"{playerName} se salió de la partida");
    }

    private void OnConnectionLost() => ShowInterrupted("Se perdió la conexión con el servidor");

    /// <summary>
    /// La partida ya no puede seguir: se bloquea la mesa y solo queda regresar al menú.
    /// Si ya había terminado no se avisa nada, porque la pantalla final ya está en camino.
    /// </summary>
    private void ShowInterrupted(string message)
    {
        if (_isGameOver) return;
        _isGameOver = true;

        CloseUnoWindow();
        CanDraw = false;
        CanPassTurn = false;
        DrawCardCommand.NotifyCanExecuteChanged();
        IsColorPickerOpen = false;
        IsExitDialogOpen = false;

        InterruptedMessage = message;
        IsInterruptedNoticeOpen = true;
    }

    private void LeaveInBackground()
    {
        CloseUnoWindow();
        if (_session is null) return;

        _session.StateChanged -= ApplyState;
        _session.UnoWindowOpened -= OnUnoWindowOpened;
        _session.UnoPenalty -= OnUnoPenalty;
        _session.GameOver -= OnGameOver;
        _session.PlayerLeft -= OnPlayerLeft;
        _session.ConnectionLost -= OnConnectionLost;

        var session = _session;
        _ = Task.Run(async () =>
        {
            try
            {
                await session.LeaveAsync();
            }
            catch (Exception)
            {
                // Si el servidor ya no está, igual se sale de la partida
            }
        });
    }

    // ---------- Victoria ----------

    /// <summary>El servidor avisa que alguien se quedó sin cartas, con la duración de la partida.</summary>
    private void OnGameOver(string winnerName, TimeSpan duration)
    {
        GameDurationText = $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
        ShowVictory(GetPlayerViewModel(winnerName));
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
        CanDraw = false;
        DrawCardCommand.NotifyCanExecuteChanged();

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
    private void BackToMenu()
    {
        LeaveInBackground();
        _goToMenu();
    }

    [RelayCommand]
    private void PlayAgain()
    {
        LeaveInBackground();
        _startNewGame();
    }
}
