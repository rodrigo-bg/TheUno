using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TheUno.UI.ViewModels;

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

    public MenuViewModel(Action onCreateGame, Action onJoinGame, Action onShowHistory)
    {
        _onCreateGame = onCreateGame;
        _onJoinGame = onJoinGame;
        _onShowHistory = onShowHistory;
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public MenuViewModel() : this(() => { }, () => { }, () => { })
    {
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
