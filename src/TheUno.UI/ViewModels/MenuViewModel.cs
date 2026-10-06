using System;
using CommunityToolkit.Mvvm.Input;

namespace TheUno.UI.ViewModels;

public partial class MenuViewModel : ViewModelBase
{
    private readonly Action _onStartGame;

    public MenuViewModel(Action onStartGame)
    {
        _onStartGame = onStartGame;
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public MenuViewModel() : this(() => { })
    {
    }

    [RelayCommand]
    private void StartGame() => _onStartGame();

    // Pendiente: abrirá StatsView (tarea 5 del rol de interfaz).
    [RelayCommand]
    private void ShowHistory()
    {
    }
}
