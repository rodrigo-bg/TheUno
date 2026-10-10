using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using TheUno.Core.Contracts;

namespace TheUno.UI.ViewModels;

/// <summary>Un renglón de la tabla de clasificación, ya listo para mostrarse.</summary>
public record LeaderboardRow(
    int Position,
    string Name,
    int MatchesPlayed,
    int MatchesWon,
    int MatchesLost,
    string WinRateText,
    string AverageDurationText,
    int TotalScore)
{
    /// <summary>El primer lugar se resalta en amarillo.</summary>
    public bool IsFirst => Position == 1;
}

/// <summary>
/// Historial y estadísticas: tabla de clasificación con partidas jugadas, victorias,
/// derrotas, porcentaje de victorias, duración promedio y puntos de cada jugador.
/// Mientras no esté conectada la base de datos, se llena con datos de ejemplo.
/// </summary>
public partial class StatsViewModel : ViewModelBase
{
    private readonly Action _goToMenu;

    public StatsViewModel(Action goToMenu)
    {
        _goToMenu = goToMenu;
        LoadStats(CreateSampleStats());
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public StatsViewModel() : this(() => { })
    {
    }

    public ObservableCollection<LeaderboardRow> Rows { get; } = new();

    /// <summary>
    /// Ordena a los jugadores por victorias (y luego por porcentaje) y arma los renglones.
    /// Recibe el mismo tipo que devuelve IMatchRepository.GetLeaderboardStatsAsync.
    /// </summary>
    public void LoadStats(IEnumerable<PlayerStatsDto> stats)
    {
        Rows.Clear();

        var ordered = stats
            .OrderByDescending(s => s.MatchesWon)
            .ThenByDescending(GetWinRate)
            .ThenBy(s => s.Name);

        var position = 1;
        foreach (var s in ordered)
        {
            Rows.Add(new LeaderboardRow(
                position++,
                s.Name,
                s.MatchesPlayed,
                s.MatchesWon,
                s.MatchesLost,
                $"{GetWinRate(s):0}%",
                $"{s.AverageDurationMinutes:0.0}",
                s.TotalScore));
        }
    }

    [RelayCommand]
    private void BackToMenu() => _goToMenu();

    private static double GetWinRate(PlayerStatsDto stats) =>
        stats.MatchesPlayed == 0 ? 0 : stats.MatchesWon * 100.0 / stats.MatchesPlayed;

    // ---------- Datos de ejemplo (se quitan cuando se conecte la base de datos) ----------

    // Mismos jugadores que la semilla de la base de datos; 12 partidas en total.
    // Datos: jugadas, ganadas, perdidas, duración promedio (minutos) y puntos.
    private static IEnumerable<PlayerStatsDto> CreateSampleStats() => new[]
    {
        new PlayerStatsDto(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Rodri", 12, 4, 8, 6.4, 325),
        new PlayerStatsDto(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Tona", 12, 5, 7, 6.4, 410),
        new PlayerStatsDto(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Alejandro", 12, 3, 9, 6.4, 240)
    };
}
