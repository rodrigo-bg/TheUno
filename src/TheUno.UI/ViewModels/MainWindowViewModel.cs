using System;
using CommunityToolkit.Mvvm.ComponentModel;
using TheUno.Core.Contracts;
using TheUno.UI.Services;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Controla qué pantalla se muestra en la ventana principal (menú, tablero o historial)
/// y la música que suena en cada una. La partida vive en el servidor: aquí solo se abre su tablero.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly MusicPlayer _music;
    private readonly IMatchRepository _matchRepository;
    private readonly IGameLogRepository _logRepository;

    // Crea la conexión con la sala de cada partida
    private readonly Func<ILobbyService>? _createLobby;

    [ObservableProperty]
    private ViewModelBase _currentView;

    /// <summary>
    /// Se muestra "CARGANDO..." y se ignoran más clics.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    public MainWindowViewModel(MusicPlayer music, IMatchRepository matchRepository, IGameLogRepository logRepository,
        Func<ILobbyService>? createLobby = null)
    {
        _music = music;
        _matchRepository = matchRepository;
        _logRepository = logRepository;
        _createLobby = createLobby;
        _currentView = CreateMenu();
        PlayMusicFor(_currentView);
    }

    partial void OnCurrentViewChanged(ViewModelBase value) => PlayMusicFor(value);

    // La partida tiene música tranquila; el menú y las demás pantallas, la movida.
    private void PlayMusicFor(ViewModelBase view) =>
        _music.Play(view is BoardViewModel ? MusicPlayer.BoardTrack : MusicPlayer.MenuTrack);

    private MenuViewModel CreateMenu() => new(() => ShowLobby(isHost: true), () => ShowLobby(isHost: false), ShowHistory);

    /// <summary>
    /// Crear o unirse a partida: cada vez es una sala nueva. Al volver a jugar se regresa aquí
    /// con el mismo papel (quien creó la partida la vuelve a crear; quien se unió se reconecta a la misma IP).
    /// </summary>
    private void ShowLobby(bool isHost, string? reconnectIp = null)
    {
        LobbyViewModel? lobby = null;
        lobby = new LobbyViewModel(_createLobby?.Invoke(), isHost, ShowMenu,
            _ => StartGame(lobby!, () => ShowLobby(isHost, lobby!.ServerIp)), reconnectIp);
        CurrentView = lobby;
    }

    /// <summary>
    /// La sala avisa que ya empezó: se abre el tablero con la partida del servidor.
    /// "Volver a jugar" en la pantalla final llama a playAgain, que regresa a la sala.
    /// </summary>
    private void StartGame(LobbyViewModel lobby, Action playAgain)
    {
        if (lobby.Session is not { } session) return;

        CurrentView = new BoardViewModel(session, ShowMenu, playAgain);
    }

    private void ShowHistory()
    {
        CurrentView = new StatsViewModel(ShowMenu, _matchRepository, _logRepository);
    }

    // Al regresar se crea un menú nuevo; la partida o el historial anterior se descartan.
    private void ShowMenu()
    {
        CurrentView = CreateMenu();
    }
}
