using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TheUno.Core.Contracts;
using TheUno.Infrastructure.Repositories;
using TheUno.UI.Services;
using TheUno.UI.ViewModels;

namespace TheUno.UI;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

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

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. Configurar los servicios e inyección de dependencias
        var serviceCollection = new ServiceCollection();

        // Registrar repositorios HTTP apuntando a FastAPI (127.0.0.1 en lugar de localhost: en Windows
        // localhost también intenta IPv6 y tarda más en fallar cuando la API está cerrada).
        // Si la API no responde, a los 3 segundos se deja de esperar y el juego sigue sin guardar.
        serviceCollection.AddHttpClient<IMatchRepository, ApiMatchRepository>(client =>
        {
            client.BaseAddress = new Uri("http://127.0.0.1:8000/");
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        serviceCollection.AddHttpClient<IGameLogRepository, ApiGameLogRepository>(client =>
        {
            client.BaseAddress = new Uri("http://127.0.0.1:8000/");
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        // La auditoría la hace el propio GameEngine con IGameLogRepository (sin GameAuditService, para no duplicar)
        Services = serviceCollection.BuildServiceProvider();

        // 2. Inicializar la ventana principal
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    Music,
                    Services.GetRequiredService<IMatchRepository>(),
                    Services.GetRequiredService<IGameLogRepository>(),
                    LobbyFactory)
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