using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheUno.Core.Models;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Estado visual de la mesa. Mientras TheUno.Core no tenga el motor de juego,
/// se llena con datos de ejemplo para poder visualizar el tablero.
/// </summary>
public partial class BoardViewModel : ViewModelBase
{
    // Datos de ejemplo
    private const int InitialHandSize = 7;
    private static readonly CardColor[] SampleColors = { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow };
    private static readonly CardType[] SampleActionTypes = { CardType.Skip, CardType.Reverse, CardType.DrawTwo };
    private readonly Random _random = new();

    // Navegación: las pone MainWindowViewModel
    private readonly Action _goToMenu;
    private readonly Action _startNewGame;

    // Qué hacer con el color elegido (cuando exista el motor: llamar a PlayCard con ese color).
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

        Player1 = new PlayerViewModel("Jugador 1");
        Player2 = new PlayerViewModel("Jugador 2");
        Player3 = new PlayerViewModel("Jugador 3");

        foreach (var player in new[] { Player1, Player2, Player3 })
        {
            for (var i = 0; i < InitialHandSize; i++)
            {
                player.Hand.Add(CreateRandomCard());
            }
        }

        _topDiscardCard = new CardViewModel(CreateRandomNumberCard());
        _activeColor = _topDiscardCard.Card.Color;

        // Datos de ejemplo: la partida empieza con el turno del Jugador 1.
        _activePlayer = Player1;
        Player1.IsActive = true;
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
        // Cuando exista el motor, aquí se llamará a CallUno() de IGameEngine.
        player.HasCalledUno = true;
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

    /// <summary>
    /// Termina la partida: tras una pausa de 3 segundos (para que se vea la última carta en la mesa)
    /// aparece la pantalla de victoria con el nombre del ganador.
    /// Cuando exista el motor, se llamará desde su evento OnGameOver.
    /// </summary>
    public void ShowVictory(PlayerViewModel winner)
    {
        if (_isGameOver) return;
        _isGameOver = true;

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

    // ---------- Datos de ejemplo (se quitan cuando exista el motor) ----------

    private CardViewModel CreateRandomCard()
    {
        // 1 de cada 14 cartas es comodín, igual que en la baraja real (8 de 108).
        if (_random.Next(14) == 0)
        {
            var wildType = _random.Next(2) == 0 ? CardType.Wild : CardType.WildDrawFour;
            return new CardViewModel(new Card(Guid.NewGuid(), CardColor.None, wildType));
        }

        // 10 números y 3 cartas de acción por color.
        var roll = _random.Next(13);
        if (roll < 10)
        {
            return new CardViewModel(CreateRandomNumberCard());
        }

        return new CardViewModel(new Card(Guid.NewGuid(), PickRandomColor(), SampleActionTypes[roll - 10]));
    }

    private Card CreateRandomNumberCard() =>
        new(Guid.NewGuid(), PickRandomColor(), CardType.Number, _random.Next(10));

    private CardColor PickRandomColor() => SampleColors[_random.Next(SampleColors.Length)];
}
