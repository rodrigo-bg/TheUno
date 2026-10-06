using CommunityToolkit.Mvvm.ComponentModel;
using TheUno.UI.Services;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Controla qué pantalla se muestra en la ventana principal (menú o tablero)
/// y la música que suena en cada una.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly MusicPlayer _music;

    [ObservableProperty]
    private ViewModelBase _currentView;

    public MainWindowViewModel(MusicPlayer music)
    {
        _music = music;
        _currentView = new MenuViewModel(StartGame);
        PlayMusicFor(_currentView);
    }

    partial void OnCurrentViewChanged(ViewModelBase value) => PlayMusicFor(value);

    // La partida tiene música tranquila; el menú y las demás pantallas, la movida.
    private void PlayMusicFor(ViewModelBase view) =>
        _music.Play(view is BoardViewModel ? MusicPlayer.BoardTrack : MusicPlayer.MenuTrack);

    private void StartGame()
    {
        CurrentView = new BoardViewModel();
    }
}
