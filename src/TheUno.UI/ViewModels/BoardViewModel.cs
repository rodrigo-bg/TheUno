using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Estado visual de la mesa. Mientras TheUno.Core no tenga el motor de juego,
/// se llena con datos de ejemplo para poder visualizar el tablero.
/// </summary>
public partial class BoardViewModel : ViewModelBase
{
    private const int InitialHandSize = 7;

    private static readonly string[] Colors = { "r", "g", "b", "y" };
    private static readonly string[] Values =
        { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "skip", "reverse", "draw2" };

    private readonly Random _random = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveColorBrush))]
    private char _activeColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCounterClockwise))]
    [NotifyPropertyChangedFor(nameof(DirectionText))]
    private bool _isClockwise = true;

    [ObservableProperty]
    private CardViewModel _topDiscard;

    public BoardViewModel()
    {
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

        _topDiscard = new CardViewModel(Colors[_random.Next(Colors.Length)] + "_" + Values[_random.Next(10)]);
        _activeColor = _topDiscard.ColorCode ?? 'r';
    }

    public PlayerViewModel Player1 { get; }
    public PlayerViewModel Player2 { get; }
    public PlayerViewModel Player3 { get; }

    public Bitmap DrawPileImage => CardViewModel.BackImage;

    public bool IsCounterClockwise => !IsClockwise;

    public string DirectionText => IsClockwise ? "Horario" : "Antihorario";

    public IBrush ActiveColorBrush => ActiveColor switch
    {
        'r' => new SolidColorBrush(Color.Parse("#E53935")),
        'g' => new SolidColorBrush(Color.Parse("#43A047")),
        'b' => new SolidColorBrush(Color.Parse("#1E88E5")),
        'y' => new SolidColorBrush(Color.Parse("#FDD835")),
        _ => new SolidColorBrush(Color.Parse("#555555"))
    };

    private CardViewModel RandomCard()
    {
        // 1 de cada 14 cartas es comodín, igual que en la baraja real (8 de 108).
        if (_random.Next(14) == 0)
        {
            return new CardViewModel(_random.Next(2) == 0 ? "wild" : "wild_draw4");
        }

        var color = Colors[_random.Next(Colors.Length)];
        var value = Values[_random.Next(Values.Length)];
        return new CardViewModel($"{color}_{value}");
    }
}
