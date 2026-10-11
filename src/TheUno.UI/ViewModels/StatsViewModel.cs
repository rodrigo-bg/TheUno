using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
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
    string WinRateText)
{
    /// <summary>El primer lugar se resalta en amarillo.</summary>
    public bool IsFirst => Position == 1;
}

/// <summary>Una partida terminada en la lista del historial.</summary>
public record MatchRow(Guid MatchId, string DateText, string WinnerName, string DetailText);

/// <summary>Un movimiento de la bitácora: número de turno y descripción en español.</summary>
public record LogRow(string TurnText, string Text);

/// <summary>
/// Historial y estadísticas: tabla de clasificación con partidas jugadas, victorias,
/// derrotas y porcentaje de victorias de cada jugador, la lista de partidas terminadas
/// y la bitácora de movimientos de la partida elegida.
/// Los datos se piden a la API; en el diseñador de Rider se usan datos de ejemplo.
/// </summary>
public partial class StatsViewModel : ViewModelBase
{
    private readonly Action _goToMenu;
    private readonly IMatchRepository? _matchRepository;
    private readonly IGameLogRepository? _logRepository;

    // Nombre de cada jugador por su Id, para escribir la bitácora (los movimientos solo guardan el Id)
    private readonly Dictionary<Guid, string> _playerNames = new();

    /// <summary>Aviso en lugar de la tabla: cargando, sin conexión o sin partidas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    /// <summary>Partida elegida en la lista: al cambiar se carga su bitácora.</summary>
    [ObservableProperty]
    private MatchRow? _selectedMatch;

    /// <summary>Aviso en lugar de la bitácora: elegir partida, cargando, sin movimientos o sin conexión.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogMessage))]
    private string _logMessage = "Elige una partida para ver sus movimientos";

    public StatsViewModel(Action goToMenu, IMatchRepository matchRepository, IGameLogRepository logRepository)
    {
        _goToMenu = goToMenu;
        _matchRepository = matchRepository;
        _logRepository = logRepository;
        _ = LoadFromRepositoryAsync();
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public StatsViewModel()
    {
        _goToMenu = () => { };
        LoadStats(CreateSampleStats());
        LoadMatches(CreateSampleMatches());
        LoadLogs(CreateSampleLogs());
    }

    public bool HasStatusMessage => StatusMessage.Length > 0;

    public bool HasLogMessage => LogMessage.Length > 0;

    public ObservableCollection<LeaderboardRow> Rows { get; } = new();

    public ObservableCollection<MatchRow> Matches { get; } = new();

    public ObservableCollection<LogRow> LogRows { get; } = new();

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
            _playerNames[s.PlayerId] = s.Name;
            Rows.Add(new LeaderboardRow(
                position++,
                s.Name,
                s.MatchesPlayed,
                s.MatchesWon,
                s.MatchesLost,
                $"{GetWinRate(s):0}%"));
        }
    }

    /// <summary>Partidas terminadas, de la más reciente a la más antigua (así las entrega el repositorio).</summary>
    public void LoadMatches(IEnumerable<MatchSummaryDto> matches)
    {
        Matches.Clear();
        foreach (var m in matches)
        {
            // La API y la base guardan la hora en UTC
            var startedAt = DateTime.SpecifyKind(m.StartedAt, DateTimeKind.Utc).ToLocalTime();
            var duration = TimeSpan.FromMinutes(m.DurationMinutes);
            Matches.Add(new MatchRow(
                m.MatchId,
                startedAt.ToString("dd/MM HH:mm"),
                m.WinnerName,
                $"{(int)duration.TotalMinutes}:{duration.Seconds:00} min · {m.TotalTurns} turnos"));
        }

        if (Matches.Count == 0) LogMessage = "Todavía no hay partidas terminadas";
    }

    /// <summary>Convierte los movimientos guardados en renglones de la bitácora.</summary>
    public void LoadLogs(IEnumerable<GameLogEntryDto> logs)
    {
        LogRows.Clear();
        foreach (var log in logs)
        {
            LogRows.Add(new LogRow($"T{log.TurnNumber}", DescribeLog(log)));
        }

        LogMessage = LogRows.Count == 0 ? "Esta partida no tiene movimientos guardados" : string.Empty;
    }

    private async Task LoadFromRepositoryAsync()
    {
        if (_matchRepository is null) return;

        StatusMessage = "Cargando...";
        try
        {
            LoadStats(await _matchRepository.GetLeaderboardStatsAsync());
            LoadMatches(await _matchRepository.GetCompletedMatchesAsync());
            StatusMessage = string.Empty;
        }
        catch (Exception)
        {
            StatusMessage = "No se pudo conectar con la API";
            LogMessage = string.Empty;
        }
    }

    partial void OnSelectedMatchChanged(MatchRow? value)
    {
        if (value is not null) _ = LoadLogsAsync(value.MatchId);
    }

    private async Task LoadLogsAsync(Guid matchId)
    {
        if (_logRepository is null) return;

        LogRows.Clear();
        LogMessage = "Cargando...";
        try
        {
            var logs = await _logRepository.GetLogsByMatchAsync(matchId);
            // Si mientras cargaba se eligió otra partida, esta respuesta ya no sirve
            if (SelectedMatch?.MatchId == matchId) LoadLogs(logs);
        }
        catch (Exception)
        {
            if (SelectedMatch?.MatchId == matchId) LogMessage = "No se pudo conectar con la API";
        }
    }

    [RelayCommand]
    private void BackToMenu() => _goToMenu();

    private static double GetWinRate(PlayerStatsDto stats) =>
        stats.MatchesPlayed == 0 ? 0 : stats.MatchesWon * 100.0 / stats.MatchesPlayed;

    // ---------- Textos de la bitácora ----------

    private string DescribeLog(GameLogEntryDto log)
    {
        var name = _playerNames.TryGetValue(log.PlayerId, out var playerName) ? playerName : "Jugador";

        return log.Action switch
        {
            GameActionType.PlayCard when log.ChosenColor is { Length: > 0 } color =>
                $"{name} tiró {TranslateCard(log.CardDetails)} y eligió {TranslateColor(color)}",
            GameActionType.PlayCard => $"{name} tiró {TranslateCard(log.CardDetails)}",
            GameActionType.DrawCard => $"{name} robó {TranslateCard(log.CardDetails)}",
            GameActionType.PassTurn => $"{name} pasó el turno",
            GameActionType.CallUno => $"{name} cantó UNO",
            GameActionType.UnoPenalty => $"{name} no cantó UNO y recibió 2 cartas",
            GameActionType.GameOver => DescribeGameOver(name, log.CardDetails),
            _ => $"{name}: {log.CardDetails}"
        };
    }

    // El motor guarda "Victoria con 125 puntos"
    private static string DescribeGameOver(string name, string details)
    {
        var points = Regex.Match(details, @"\d+");
        return points.Success ? $"{name} ganó la partida con {points.Value} puntos" : $"{name} ganó la partida";
    }

    // El motor guarda las cartas en inglés: "Red 7", "Blue Skip", "Green DrawTwo", "Wild", "Wild Draw 4 (+4)"
    private static string TranslateCard(string card)
    {
        if (card.StartsWith("Wild Draw")) return "Comodín +4";
        if (card == "Wild") return "Comodín";

        var parts = card.Split(' ', 2);
        if (parts.Length < 2) return card;

        var value = parts[1] switch
        {
            "Skip" => "Salto",
            "Reverse" => "Reversa",
            "DrawTwo" => "+2",
            _ => parts[1]
        };
        return $"{TranslateColor(parts[0])} {value}";
    }

    private static string TranslateColor(string color) => color switch
    {
        "Red" => "Rojo",
        "Blue" => "Azul",
        "Green" => "Verde",
        "Yellow" => "Amarillo",
        _ => color
    };

    // ---------- Datos de ejemplo (solo para el diseñador) ----------

    // Mismos jugadores que la semilla de la base de datos; 12 partidas en total.
    // Datos: jugadas, ganadas, perdidas, duración promedio (minutos) y puntos.
    private static IEnumerable<PlayerStatsDto> CreateSampleStats() => new[]
    {
        new PlayerStatsDto(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Rodri", 12, 4, 8, 6.4, 325),
        new PlayerStatsDto(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Tona", 12, 5, 7, 6.4, 410),
        new PlayerStatsDto(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Alejandro", 12, 3, 9, 6.4, 240)
    };

    private static IEnumerable<MatchSummaryDto> CreateSampleMatches() => new[]
    {
        new MatchSummaryDto(Guid.NewGuid(), new DateTime(2026, 10, 10, 20, 32, 0, DateTimeKind.Utc), "Tona", 5.25, 31),
        new MatchSummaryDto(Guid.NewGuid(), new DateTime(2026, 10, 10, 20, 10, 0, DateTimeKind.Utc), "Rodri", 7.5, 44)
    };

    private static IEnumerable<GameLogEntryDto> CreateSampleLogs()
    {
        var match = Guid.NewGuid();
        var rodri = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tona = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var alejandro = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var time = DateTime.UtcNow;
        return new[]
        {
            new GameLogEntryDto(match, 1, tona, GameActionType.PlayCard, "Red 7", null, time),
            new GameLogEntryDto(match, 2, rodri, GameActionType.DrawCard, "Blue 2", null, time),
            new GameLogEntryDto(match, 3, alejandro, GameActionType.PlayCard, "Wild", "Blue", time),
            new GameLogEntryDto(match, 9, rodri, GameActionType.UnoPenalty, "Penalizacion de 2 cartas por no cantar UNO", null, time),
            new GameLogEntryDto(match, 31, tona, GameActionType.GameOver, "Victoria con 125 puntos", null, time)
        };
    }
}
