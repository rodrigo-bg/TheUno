using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TheUno.Core.Models;

namespace TheUno.UI.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    /// <summary>Indica si es el turno de este jugador: su zona se resalta y su mano se habilita.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Indica si el jugador ya cantó UNO; junto a su nombre aparece la etiqueta "¡UNO!".</summary>
    [ObservableProperty]
    private bool _hasCalledUno;

    /// <summary>
    /// Muestra el botón ¡UNO! de este jugador. Lo prende el tablero durante los 2 segundos
    /// que siguen a tirar su penúltima carta.
    /// </summary>
    [ObservableProperty]
    private bool _showUnoButton;

    public PlayerViewModel(Player model)
    {
        Model = model;
        Hand.CollectionChanged += OnHandChanged;
        SyncHand();
    }

    /// <summary>Jugador del motor de juego al que representa este ViewModel.</summary>
    public Player Model { get; }

    public string Name => Model.Name;

    public ObservableCollection<CardViewModel> Hand { get; } = new();

    /// <summary>
    /// Iguala la mano visible con la del motor: quita las cartas que ya no tiene y agrega las nuevas.
    /// Las cartas que se quedan conservan su CardViewModel, así no se reconstruye toda la mano.
    /// </summary>
    public void SyncHand()
    {
        for (var i = Hand.Count - 1; i >= 0; i--)
        {
            if (!Model.Hand.Contains(Hand[i].Card)) Hand.RemoveAt(i);
        }

        foreach (var card in Model.Hand)
        {
            if (Hand.All(c => c.Card != card)) Hand.Add(new CardViewModel(card));
        }
    }

    private void OnHandChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Si vuelve a tener más de 1 carta (por ejemplo, al robar), el UNO cantado ya no cuenta
        // y tendrá que volver a cantarlo cuando le quede 1.
        if (Hand.Count > 1) HasCalledUno = false;
    }
}
