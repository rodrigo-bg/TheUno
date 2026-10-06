using System;
using System.IO;
using LibVLCSharp.Shared;

namespace TheUno.UI.Services;

/// <summary>
/// Reproduce la música de fondo en loop durante toda la aplicación (menú y partida).
/// </summary>
public sealed class MusicPlayer : IDisposable
{
    private static readonly string MusicPath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "musica_8bit.mp3");

    // Repeticiones máximas que acepta LibVLC: con una canción de minutos equivale a no detenerse nunca.
    private const int RepeatCount = 65535;

    private LibVLC? _libVlc;
    private MediaPlayer? _player;

    public void Start(int volume = 60)
    {
        // Si LibVLC no está disponible (por ejemplo fuera de Windows) el juego sigue sin música.
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC("--no-video");

            using var media = new Media(_libVlc, MusicPath, FromType.FromPath);
            media.AddOption($":input-repeat={RepeatCount}");

            _player = new MediaPlayer(media) { Volume = volume };
            _player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo reproducir la música: {ex.Message}");
            Dispose();
        }
    }

    public void Dispose()
    {
        _player?.Stop();
        _player?.Dispose();
        _player = null;

        _libVlc?.Dispose();
        _libVlc = null;
    }
}
