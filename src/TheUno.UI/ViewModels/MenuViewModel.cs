using System;
using CommunityToolkit.Mvvm.Input;

namespace TheUno.UI.ViewModels;

public partial class MenuViewModel : ViewModelBase
{
    private readonly Action _onStartGame;
    private readonly Action _onShowHistory;

    public MenuViewModel(Action onStartGame, Action onShowHistory)
    {
        _onStartGame = onStartGame;
        _onShowHistory = onShowHistory;
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public MenuViewModel() : this(() => { }, () => { })
    {
    }

    [RelayCommand]
    private void StartGame() => _onStartGame();

    /// <summary>Abre la pantalla de historial y estadísticas.</summary>
    [RelayCommand]
    private void ShowHistory() => _onShowHistory();
}
