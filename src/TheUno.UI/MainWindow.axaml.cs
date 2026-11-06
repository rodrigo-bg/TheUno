using Avalonia.Controls;
using Avalonia.Input;

namespace TheUno.UI;

public partial class MainWindow : Window
{
    // Cómo estaba la ventana antes de la pantalla completa, para regresarla igual
    private WindowState _stateBeforeFullScreen;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>F11 entra y sale de la pantalla completa en cualquier pantalla del juego.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key != Key.F11) return;

        if (WindowState == WindowState.FullScreen)
        {
            WindowState = _stateBeforeFullScreen;
        }
        else
        {
            _stateBeforeFullScreen = WindowState;
            WindowState = WindowState.FullScreen;
        }

        e.Handled = true;
    }
}
