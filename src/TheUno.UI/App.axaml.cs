using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TheUno.Core.Contracts;
using TheUno.Infrastructure.Repositories;
using TheUno.Infrastructure.Services;
using TheUno.UI.Services;
using TheUno.UI.ViewModels;

namespace TheUno.UI;

public partial class App : Application
{
    private readonly MusicPlayer _music = new();
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. Configurar los servicios e inyección de dependencias
        var serviceCollection = new ServiceCollection();

        // Registrar repositorios HTTP apuntando a FastAPI
        serviceCollection.AddHttpClient<IMatchRepository, ApiMatchRepository>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:8000/");
        });

        serviceCollection.AddHttpClient<IGameLogRepository, ApiGameLogRepository>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:8000/");
        });

        // Registrar servicio de auditoría
        serviceCollection.AddSingleton<GameAuditService>();

        Services = serviceCollection.BuildServiceProvider();

        // 2. Inicializar la ventana principal
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(_music)
            };

            desktop.Exit += (_, _) => _music.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}