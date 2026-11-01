using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheUno.UI.Services;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Pantallas para entrar a una partida en red. Quien la crea ve su IP y elige jugador;
/// quien se une escribe la IP del servidor, se conecta y luego elige jugador.
/// Un jugador que ya eligió alguien más no se puede volver a elegir.
/// </summary>
public partial class LobbyViewModel : ViewModelBase
{
    /// <summary>Los 3 jugadores de la partida, en el orden en que se muestran.</summary>
    public static readonly string[] PlayerNames = { "RODRIGO", "TONATIUH", "ALEJANDRO" };

    // Sin servicio todavía no hay servidor con quien hablar: la sala se queda esperando
    private readonly ILobbyService? _lobby;
    private readonly Action _goToMenu;

    /// <summary>Se está esperando al servidor: los botones no responden para no mandar dos veces.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChoosePlayerCommand))]
    private bool _isBusy;

    /// <summary>Quien se une ya se conectó al servidor: puede elegir jugador.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnectForm))]
    [NotifyPropertyChangedFor(nameof(ShowPlayerPicker))]
    private bool _isConnected;

    /// <summary>Ya eligió jugador: pasa a la sala de conexión.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlayerPicker))]
    private bool _isInRoom;

    /// <summary>IP que escribe quien se une.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _serverIp = string.Empty;

    /// <summary>Aviso debajo del formulario (conexión exitosa o un error).</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>El aviso es un error: se pinta en rojo en lugar de verde.</summary>
    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>Jugador que eligió esta compu.</summary>
    [ObservableProperty]
    private string? _myPlayerName;

    public LobbyViewModel(ILobbyService? lobby, bool isHost, Action goToMenu)
    {
        _lobby = lobby;
        _goToMenu = goToMenu;
        IsHost = isHost;
        LocalIp = isHost ? NetworkInfo.GetLocalIPv4() ?? "SIN RED" : string.Empty;
        PlayerOptions = new ObservableCollection<PlayerOption>(PlayerNames.Select(n => new PlayerOption(n)));

        if (_lobby is not null) _lobby.PlayersChanged += UpdateTakenPlayers;
        UpdateTakenPlayers();
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public LobbyViewModel() : this(null, true, () => { })
    {
    }

    /// <summary>Esta compu creó la partida (es el servidor).</summary>
    public bool IsHost { get; }

    public string Title => IsHost ? "CREAR PARTIDA" : "UNIRSE A PARTIDA";

    /// <summary>IP de esta compu, la que los demás escriben para unirse. Solo la ve quien crea la partida.</summary>
    public string LocalIp { get; }

    public ObservableCollection<PlayerOption> PlayerOptions { get; }

    public bool ShowConnectForm => !IsHost && !IsConnected;

    public bool ShowPlayerPicker => (IsHost || IsConnected) && !IsInRoom;

    // ---------- Unirse ----------

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        try
        {
            if (_lobby is null) throw new InvalidOperationException("No hay servidor.");

            await _lobby.ConnectAsync(ServerIp.Trim());
            IsConnected = true;
            ShowStatus("¡Conexión exitosa!", isError: false);
        }
        catch (Exception)
        {
            ShowStatus("No se pudo conectar a esa IP", isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanConnect() => !IsBusy && !string.IsNullOrWhiteSpace(ServerIp);

    // ---------- Elegir jugador ----------

    [RelayCommand(CanExecute = nameof(CanChoosePlayer))]
    private async Task ChoosePlayerAsync(PlayerOption option)
    {
        if (option.IsTaken) return;

        IsBusy = true;
        try
        {
            if (_lobby is not null)
            {
                // Quien crea la partida la abre al elegir su jugador, así ya tiene nombre al entrar a la sala
                if (IsHost) await _lobby.CreateGameAsync();

                if (!await _lobby.ChoosePlayerAsync(option.Name))
                {
                    UpdateTakenPlayers();
                    ShowStatus("Ese jugador ya lo eligió alguien más", isError: true);
                    return;
                }
            }

            MyPlayerName = option.Name;
            StatusMessage = string.Empty;
            IsInRoom = true;
        }
        catch (Exception)
        {
            ShowStatus("Se perdió la conexión con el servidor", isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanChoosePlayer(PlayerOption? option) => !IsBusy;

    /// <summary>Desactiva los jugadores que ya eligió alguien.</summary>
    private void UpdateTakenPlayers()
    {
        var joined = _lobby?.JoinedPlayers ?? Array.Empty<string>();
        foreach (var option in PlayerOptions)
        {
            option.IsTaken = joined.Contains(option.Name);
        }
    }

    // ---------- Salir ----------

    [RelayCommand]
    private async Task BackToMenuAsync()
    {
        if (_lobby is not null)
        {
            _lobby.PlayersChanged -= UpdateTakenPlayers;
            try
            {
                await _lobby.LeaveAsync();
            }
            catch (Exception)
            {
                // Si el servidor ya no está, igual se regresa al menú
            }
        }

        _goToMenu();
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}

/// <summary>Botón de un jugador en el selector: se desactiva si alguien más ya lo eligió.</summary>
public partial class PlayerOption : ObservableObject
{
    [ObservableProperty]
    private bool _isTaken;

    public PlayerOption(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
