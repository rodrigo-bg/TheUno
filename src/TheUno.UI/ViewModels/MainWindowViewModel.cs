using CommunityToolkit.Mvvm.ComponentModel;
using TheUno.UI.Services;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Controla qué pantalla se muestra en la ventana principal (menú, tablero o historial)
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
        _currentView = CreateMenu();
        PlayMusicFor(_currentView);
    }

    partial void OnCurrentViewChanged(ViewModelBase value) => PlayMusicFor(value);

    // La partida tiene música tranquila; el menú y las demás pantallas, la movida.
    private void PlayMusicFor(ViewModelBase view) =>
        _music.Play(view is BoardViewModel ? MusicPlayer.BoardTrack : MusicPlayer.MenuTrack);

    private MenuViewModel CreateMenu() => new(StartGame, ShowHistory);

    private void StartGame()
    {
        // "Volver a jugar" en la pantalla de victoria vuelve a llamar a StartGame: partida nueva.
        CurrentView = new BoardViewModel(ShowMenu, StartGame);
    }

    private void ShowHistory()
    {
        CurrentView = new StatsViewModel(ShowMenu);
    }

    // Al regresar se crea un menú nuevo; la partida o el historial anterior se descartan.
    private void ShowMenu()
    {
        CurrentView = CreateMenu();
    }
}
