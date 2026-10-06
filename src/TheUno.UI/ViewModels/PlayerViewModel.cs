using System.Collections.ObjectModel;

namespace TheUno.UI.ViewModels;

public class PlayerViewModel : ViewModelBase
{
    public PlayerViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public ObservableCollection<CardViewModel> Hand { get; } = new();
}
