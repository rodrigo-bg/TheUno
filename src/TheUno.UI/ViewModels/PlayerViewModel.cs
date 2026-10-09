using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TheUno.UI.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    /// <summary>Indica si es el turno de este jugador: su zona se resalta y su mano se habilita.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Indica si el jugador ya cantó UNO; junto a su nombre aparece la etiqueta "¡UNO!".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnoButton))]
    private bool _hasCalledUno;

    public PlayerViewModel(string name)
    {
        Name = name;
        Hand.CollectionChanged += OnHandChanged;
    }

    public string Name { get; }

    public ObservableCollection<CardViewModel> Hand { get; } = new();

    /// <summary>
    /// El botón ¡UNO! de este jugador solo aparece cuando le queda 1 carta y todavía no lo ha cantado.
    /// No depende del turno: al tirar la penúltima carta el turno ya pasó al siguiente jugador.
    /// </summary>
    public bool ShowUnoButton => Hand.Count == 1 && !HasCalledUno;

    private void OnHandChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Si vuelve a tener más de 1 carta (por ejemplo, al robar), el UNO cantado ya no cuenta
        // y tendrá que volver a cantarlo cuando le quede 1.
        if (Hand.Count > 1) HasCalledUno = false;
        OnPropertyChanged(nameof(ShowUnoButton));
    }
}
