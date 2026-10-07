using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TheUno.UI.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    /// <summary>Indica si es el turno de este jugador: su zona se resalta y su mano se habilita.</summary>
    [ObservableProperty]
    private bool _isActive;

    public PlayerViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public ObservableCollection<CardViewModel> Hand { get; } = new();
}
