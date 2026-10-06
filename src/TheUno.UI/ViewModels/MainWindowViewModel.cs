using CommunityToolkit.Mvvm.ComponentModel;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Controla qué pantalla se muestra en la ventana principal (menú o tablero).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentView;

    public MainWindowViewModel()
    {
        _currentView = new MenuViewModel(StartGame);
    }

    private void StartGame()
    {
        CurrentView = new BoardViewModel();
    }
}
