using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TheUno.UI.Services;
using TheUno.UI.ViewModels;

namespace TheUno.UI;

public partial class App : Application
{
    /// <summary>Música de fondo de toda la app; el tablero la pausa mientras suena victoria o derrota.</summary>
    public static MusicPlayer Music { get; } = new();

    /// <summary>Efectos de sonido de toda la app (botones y cartas).</summary>
    public static SoundEffects Sounds { get; } = new();

    /// <summary>Silencia todo el juego: la música y los efectos.</summary>
    public static bool IsMuted
    {
        get => Music.IsMuted;
        set
        {
            Music.IsMuted = value;
            Sounds.IsMuted = value;
        }
    }

    /// <summary>
    /// Crea la conexión con la sala de cada partida en red. Por defecto es la del servidor real;
    /// se puede cambiar antes de que arranque la app.
    /// </summary>
    public static Func<ILobbyService> LobbyFactory { get; set; } = () => new SignalRLobbyService();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Abre la ventana principal. El cliente ya no guarda partidas: eso lo hace el servidor,
    /// y el historial crea sus propios repositorios con la IP del servidor (ver ServerAddress).
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(Music, LobbyFactory)
            };

            desktop.Exit += (_, _) =>
            {
                Music.Dispose();
                Sounds.Dispose();
            };
        }

        // Todos los botones de todas las pantallas suenan al picarlos, menos las cartas y el mazo:
        // esos tienen su propio sonido cuando la carta de verdad se tira o se roba.
        Button.ClickEvent.AddClassHandler<Button>((button, _) =>
        {
            if (button.Classes.Contains("cardButton") || button.Classes.Contains("drawPile")) return;
            Sounds.Play(SoundEffects.ButtonClick);
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        base.OnFrameworkInitializationCompleted();
    }
}
