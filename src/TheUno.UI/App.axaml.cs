using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TheUno.Core.Contracts;
using TheUno.Infrastructure.Repositories;
using TheUno.UI.Services;
using TheUno.UI.ViewModels;

namespace TheUno.UI;

public partial class App : Application
{
    private readonly MusicPlayer _music = new();
    public static IServiceProvider? Services { get; private set; }

    /// <summary>
    /// Crea la conexión con la sala de cada partida en red. Mientras no exista el servidor
    /// queda vacía y la sala solo espera; se puede poner antes de que arranque la app.
    /// </summary>
    public static Func<ILobbyService>? LobbyFactory { get; set; }

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
                    _music,
                    Services.GetRequiredService<IMatchRepository>(),
                    Services.GetRequiredService<IGameLogRepository>(),
                    LobbyFactory)
            };

            desktop.Exit += (_, _) => _music.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}