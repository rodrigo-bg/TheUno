using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    private const int InitialHandSize = 7;

    private static readonly CardColor[] Colors = { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow };
    private static readonly CardType[] ActionTypes = { CardType.Skip, CardType.Reverse, CardType.DrawTwo };

    private readonly Random _random = new();
    private readonly Action _onExitToMenu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveColorBrush))]
    private char _activeColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCounterClockwise))]
    [NotifyPropertyChangedFor(nameof(DirectionText))]
    private bool _isClockwise = true;

    [ObservableProperty]
    private CardViewModel _topDiscard;

    /// <summary>Jugador en turno. Al cambiar, se actualiza qué zona se resalta y qué manos se atenúan.</summary>
    [ObservableProperty]
    private PlayerViewModel _activePlayer;

    /// <summary>Muestra la alerta que confirma si se quiere abandonar la partida.</summary>
    [ObservableProperty]
    private bool _isExitDialogOpen;

    /// <summary>Muestra el selector de color que aparece al tirar un comodín o un +4.</summary>
    [ObservableProperty]
    private bool _isColorPickerOpen;

    // Qué hacer con el color elegido (cuando exista el motor: llamar a PlayCard con ese color).
    private Action<CardColor>? _onColorChosen;

    public BoardViewModel(Action onExitToMenu)
    {
        _onExitToMenu = onExitToMenu;

        Player1 = new PlayerViewModel("Jugador 1");
        Player2 = new PlayerViewModel("Jugador 2");
        Player3 = new PlayerViewModel("Jugador 3");

        foreach (var player in new[] { Player1, Player2, Player3 })
        {
            for (var i = 0; i < InitialHandSize; i++)
            {
                player.Hand.Add(RandomCard());
            }
        }

        _topDiscard = new CardViewModel(NumberCard());
        _activeColor = _topDiscard.ColorCode ?? 'r';

        // Datos de ejemplo: la partida empieza con el turno del Jugador 1.
        _activePlayer = Player1;
        Player1.IsActive = true;
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public BoardViewModel() : this(() => { })
    {
    }

    public PlayerViewModel Player1 { get; }
    public PlayerViewModel Player2 { get; }
    public PlayerViewModel Player3 { get; }

    public Bitmap DrawPileImage => CardViewModel.BackImage;

    partial void OnActivePlayerChanged(PlayerViewModel? oldValue, PlayerViewModel newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        newValue.IsActive = true;
    }

    [RelayCommand]
    private void RequestExit() => IsExitDialogOpen = true;

    [RelayCommand]
    private void CancelExit() => IsExitDialogOpen = false;

    [RelayCommand]
    private void ConfirmExit()
    {
        IsExitDialogOpen = false;
        _onExitToMenu();
    }

    /// <summary>El jugador dueño del botón ¡UNO! canta UNO.</summary>
    [RelayCommand]
    private void CallUno(PlayerViewModel player)
    {
        // Cuando exista el motor, aquí se llamará a CallUno() de IGameEngine.
        player.HasCalledUno = true;
    }

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
        ActiveColor = CardViewModel.GetColorCode(color) ?? ActiveColor;

        var onChosen = _onColorChosen;
        _onColorChosen = null;
        onChosen?.Invoke(color);
    }

    public bool IsCounterClockwise => !IsClockwise;

    public string DirectionText => IsClockwise ? "Horario" : "Antihorario";

    // Paleta tomada directamente de las cartas 8 bits
    public IBrush ActiveColorBrush => ActiveColor switch
    {
        'r' => new SolidColorBrush(Color.Parse("#FF0000")),
        'g' => new SolidColorBrush(Color.Parse("#40FF00")),
        'b' => new SolidColorBrush(Color.Parse("#0020FF")),
        'y' => new SolidColorBrush(Color.Parse("#E6B400")),
        _ => new SolidColorBrush(Color.Parse("#555555"))
    };

    private CardViewModel RandomCard()
    {
        // 1 de cada 14 cartas es comodín, igual que en la baraja real (8 de 108).
        if (_random.Next(14) == 0)
        {
            var wildType = _random.Next(2) == 0 ? CardType.Wild : CardType.WildDrawFour;
            return new CardViewModel(new Card(Guid.NewGuid(), CardColor.None, wildType));
        }

        // 10 números y 3 cartas de acción por color.
        var value = _random.Next(13);
        if (value < 10)
        {
            return new CardViewModel(NumberCard());
        }

        return new CardViewModel(new Card(Guid.NewGuid(), RandomColor(), ActionTypes[value - 10]));
    }

    private Card NumberCard() => new(Guid.NewGuid(), RandomColor(), CardType.Number, _random.Next(10));

    private CardColor RandomColor() => Colors[_random.Next(Colors.Length)];
}
