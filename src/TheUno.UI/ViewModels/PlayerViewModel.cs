using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    /// <summary>Cuántas cartas tiene. Es lo único que se sabe de la mano de un rival.</summary>
    [ObservableProperty]
    private int _cardCount;

    public PlayerViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public ObservableCollection<CardViewModel> Hand { get; } = new();

    /// <summary>
    /// Iguala la mano visible con la que mandó el servidor: quita las cartas que ya no tiene y agrega las nuevas.
    /// Las cartas que se quedan conservan su CardViewModel, así no se reconstruye toda la mano.
    /// </summary>
    public void SyncHand(IReadOnlyList<Card> cards, IReadOnlyCollection<Guid> playableIds)
    {
        for (var i = Hand.Count - 1; i >= 0; i--)
        {
            if (cards.All(c => c.Id != Hand[i].Card.Id)) Hand.RemoveAt(i);
        }

        foreach (var card in cards)
        {
            if (Hand.All(c => c.Card.Id != card.Id)) Hand.Add(new CardViewModel(card));
        }

        foreach (var card in Hand)
        {
            card.IsPlayable = playableIds.Contains(card.Card.Id);
        }
    }
}
