using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Menú principal: iniciar juego (crear o unirse a una partida), ver el historial y quitar el sonido.
/// </summary>
public partial class MenuViewModel : ViewModelBase
{
    private readonly Action _onCreateGame;
    private readonly Action _onJoinGame;
    private readonly Action _onShowHistory;

    /// <summary>
    /// Ya se picó "Iniciar juego": en lugar de los botones principales se ven
    /// las opciones para crear o unirse a una partida.
    /// </summary>
    [ObservableProperty]
    private bool _isPlayOptionsOpen;

    /// <summary>El juego está en silencio. Cambia la imagen del botón de sonido.</summary>
    [ObservableProperty]
    private bool _isMuted;

    public MenuViewModel(Action onCreateGame, Action onJoinGame, Action onShowHistory)
    {
        _onCreateGame = onCreateGame;
        _onJoinGame = onJoinGame;
        _onShowHistory = onShowHistory;

        // Cada vez que se regresa al menú se crea uno nuevo: el silencio sigue como estaba
        _isMuted = App.IsMuted;
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public MenuViewModel() : this(() => { }, () => { }, () => { })
    {
    }

    /// <summary>Quita o regresa el sonido de todo el juego.</summary>
    [RelayCommand]
    private void ToggleSound()
    {
        IsMuted = !IsMuted;
        App.IsMuted = IsMuted;
    }

    [RelayCommand]
    private void ShowPlayOptions() => IsPlayOptionsOpen = true;

    /// <summary>"Volver" regresa a los botones principales.</summary>
    [RelayCommand]
    private void HidePlayOptions() => IsPlayOptionsOpen = false;

    /// <summary>Esta compu será el servidor de la partida.</summary>
    [RelayCommand]
    private void CreateGame() => _onCreateGame();

    /// <summary>Se une a la partida de otra compu escribiendo su IP.</summary>
    [RelayCommand]
    private void JoinGame() => _onJoinGame();

    /// <summary>Abre la pantalla de historial y estadísticas.</summary>
    [RelayCommand]
    private void ShowHistory() => _onShowHistory();
}
