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
/// Después todos esperan en la sala de conexión hasta que quien la creó inicia el juego.
/// </summary>
public partial class LobbyViewModel : ViewModelBase
{
    /// <summary>Los 3 jugadores de la partida, en el orden en que se muestran.</summary>
    public static readonly string[] PlayerNames = { "RODRIGO", "TONATIUH", "ALEJANDRO" };

    // Sin servicio todavía no hay servidor con quien hablar: la sala se queda esperando
    private readonly ILobbyService? _lobby;
    private readonly Action _goToMenu;

    // Pasa al tablero con el nombre del jugador de esta compu
    private readonly Action<string> _onGameStarted;

    /// <summary>Se está esperando al servidor: los botones no responden para no mandar dos veces.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChoosePlayerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartGameCommand))]
    private bool _isBusy;

    /// <summary>Quien se une ya se conectó al servidor: puede elegir jugador.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnectForm))]
    [NotifyPropertyChangedFor(nameof(ShowPlayerPicker))]
    private bool _isConnected;

    /// <summary>Ya eligió jugador: pasa a la sala de conexión.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlayerPicker))]
    [NotifyCanExecuteChangedFor(nameof(StartGameCommand))]
    private bool _isInRoom;

    /// <summary>Texto del contador de la sala, por ejemplo "JUGADORES 2/3".</summary>
    [ObservableProperty]
    private string _playerCountText = string.Empty;

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

    /// <param name="reconnectIp">
    /// Al volver a jugar, la IP a la que ya estaba unido: se conecta solo y pasa directo a elegir jugador.
    /// </param>
    public LobbyViewModel(ILobbyService? lobby, bool isHost, Action goToMenu, Action<string> onGameStarted,
        string? reconnectIp = null)
    {
        _lobby = lobby;
        _goToMenu = goToMenu;
        _onGameStarted = onGameStarted;
        IsHost = isHost;
        LocalIp = isHost ? NetworkInfo.GetLocalIPv4() ?? "SIN RED" : string.Empty;
        PlayerOptions = new ObservableCollection<PlayerOption>(PlayerNames.Select(n => new PlayerOption(n)));
        RoomSlots = new ObservableCollection<RoomSlot>(PlayerNames.Select(_ => new RoomSlot()));

        if (_lobby is not null)
        {
            _lobby.PlayersChanged += OnPlayersChanged;
            _lobby.GameStarted += OnGameStarted;
        }

        OnPlayersChanged();

        if (!isHost && !string.IsNullOrWhiteSpace(reconnectIp))
        {
            ServerIp = reconnectIp;
            ConnectCommand.Execute(null);
        }
    }

    /// <summary>Constructor para el diseñador de Rider/Avalonia.</summary>
    public LobbyViewModel() : this(null, true, () => { }, _ => { })
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

    /// <summary>Los 3 lugares de la sala: primero los que ya se unieron, después los vacíos.</summary>
    public ObservableCollection<RoomSlot> RoomSlots { get; }

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
                    OnPlayersChanged();
                    ShowStatus("Ese jugador ya lo eligió alguien más", isError: true);
                    return;
                }
            }

            MyPlayerName = option.Name;
            StatusMessage = string.Empty;
            IsInRoom = true;
            OnPlayersChanged();
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

    // ---------- Sala de conexión ----------

    /// <summary>
    /// Alguien entró o salió: se desactivan en el selector los jugadores ya elegidos
    /// y se llenan los lugares de la sala.
    /// </summary>
    private void OnPlayersChanged()
    {
        // Sin servidor, en la sala solo está el jugador de esta compu
        var joined = _lobby?.JoinedPlayers
                     ?? (MyPlayerName is null ? Array.Empty<string>() : new[] { MyPlayerName });

        foreach (var option in PlayerOptions)
        {
            option.IsTaken = joined.Contains(option.Name);
        }

        for (var i = 0; i < RoomSlots.Count; i++)
        {
            var name = i < joined.Count ? joined[i] : null;
            RoomSlots[i].Name = name;
            RoomSlots[i].IsMe = name is not null && name == MyPlayerName;
        }

        PlayerCountText = $"JUGADORES {joined.Count}/{PlayerNames.Length}";
        StartGameCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Solo quien creó la partida la inicia, y solo cuando ya están los 3.</summary>
    [RelayCommand(CanExecute = nameof(CanStartGame))]
    private async Task StartGameAsync()
    {
        IsBusy = true;
        try
        {
            // El servidor avisa a todos con GameStarted (también a esta compu)
            if (_lobby is not null) await _lobby.StartGameAsync();
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

    private bool CanStartGame() =>
        IsHost && IsInRoom && !IsBusy && RoomSlots.All(slot => slot.Name is not null);

    private void OnGameStarted()
    {
        if (MyPlayerName is null) return;

        StopListening();
        _onGameStarted(MyPlayerName);
    }

    // ---------- Salir ----------

    [RelayCommand]
    private async Task BackToMenuAsync()
    {
        if (_lobby is not null)
        {
            StopListening();
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

    private void StopListening()
    {
        if (_lobby is null) return;

        _lobby.PlayersChanged -= OnPlayersChanged;
        _lobby.GameStarted -= OnGameStarted;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}

/// <summary>Un lugar de la sala de conexión: vacío mientras nadie lo ocupa.</summary>
public partial class RoomSlot : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? _name;

    /// <summary>Es el jugador de esta compu: se marca en la lista.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private bool _isMe;

    public bool IsEmpty => Name is null;

    // En minúsculas porque la fuente pixel no tiene la "Ú" mayúscula
    public string DisplayName => IsMe ? $"{Name} (tú)" : Name ?? string.Empty;
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
