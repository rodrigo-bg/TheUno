using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TheUno.UI.Services;
using TheUno.UI.ViewModels;

namespace TheUno.UI;

public partial class App : Application
{
    private readonly MusicPlayer _music = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };

            // La música empieza en el menú y sigue sonando dentro de la partida
            _music.Start();
            desktop.Exit += (_, _) => _music.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
