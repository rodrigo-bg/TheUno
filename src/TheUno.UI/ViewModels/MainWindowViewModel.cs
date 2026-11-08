using System;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using TheUno.Infrastructure.Repositories;
using TheUno.UI.Services;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Controla qué pantalla se muestra en la ventana principal (menú, tablero o historial)
/// y la música que suena en cada una. La partida vive en el servidor: aquí solo se abre su tablero.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // Si la API no responde, a los 3 segundos se deja de esperar y el historial avisa
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(3);

    private readonly MusicPlayer _music;

    // Crea la conexión con la sala de cada partida
    private readonly Func<ILobbyService>? _createLobby;

    [ObservableProperty]
    private ViewModelBase _currentView;

    public MainWindowViewModel(MusicPlayer music, Func<ILobbyService>? createLobby = null)
    {
        _music = music;
        _createLobby = createLobby;
        _currentView = CreateMenu();
        PlayMusicFor(_currentView);
    }

    partial void OnCurrentViewChanged(ViewModelBase value) => PlayMusicFor(value);

    // La partida tiene música tranquila; el menú y las demás pantallas, la movida.
    private void PlayMusicFor(ViewModelBase view) =>
        _music.Play(view is BoardViewModel ? MusicPlayer.BoardTrack : MusicPlayer.MenuTrack);

    private MenuViewModel CreateMenu() => new(() => ShowLobby(isHost: true), () => ShowLobby(isHost: false), ShowHistory);

    // Al regresar se crea un menú nuevo; la partida o el historial anterior se descartan.
    private void ShowMenu()
    {
        CurrentView = CreateMenu();
    }

    /// <summary>"Regresar" desde la sala vuelve a las opciones de crear o unirse, no al menú principal.</summary>
    private void ShowPlayOptions()
    {
        var menu = CreateMenu();
        menu.IsPlayOptionsOpen = true;
        CurrentView = menu;
    }

    /// <summary>
    /// Crear o unirse a partida: cada vez es una sala nueva. Al volver a jugar se regresa aquí
    /// con el mismo papel (quien creó la partida la vuelve a crear; quien se unió se reconecta a la misma IP).
    /// </summary>
    private void ShowLobby(bool isHost, string? reconnectIp = null)
    {
        LobbyViewModel? lobby = null;
        lobby = new LobbyViewModel(_createLobby?.Invoke(), isHost, ShowPlayOptions,
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

    /// <summary>
    /// El historial lee la API de la compu servidor (la de la última partida; sin partida, esta compu).
    /// Los repositorios se crean con la IP del momento, también cuando se cambia desde el historial.
    /// </summary>
    private void ShowHistory()
    {
        CurrentView = new StatsViewModel(ShowMenu,
            () => new ApiMatchRepository(CreateApiClient()),
            () => new ApiGameLogRepository(CreateApiClient()));
    }

    private static HttpClient CreateApiClient() => new()
    {
        BaseAddress = ServerAddress.ApiUri,
        Timeout = ApiTimeout
    };
}
