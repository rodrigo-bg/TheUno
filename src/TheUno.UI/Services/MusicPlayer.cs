using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TheUno.UI.Services;

/// <summary>
/// Reproduce en loop la música de fondo de la pantalla actual (menú o partida).
/// En Windows usa NAudio y en macOS el reproductor afplay que trae el sistema.
/// </summary>
public sealed class MusicPlayer : IDisposable
{
    public const string MenuTrack = "Musica_crazy_8bit.mp3";
    public const string BoardTrack = "Musica_chill_8bit.mp3";

    private static readonly string AudioFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");

    private readonly float _volume;
    private string? _currentTrack;
    private string _musicPath = string.Empty;

    // Windows
    private MediaFoundationReader? _reader;
    private WasapiPlayer? _output;

    // macOS
    private Process? _afplay;

    public MusicPlayer(int volume = 60)
    {
        _volume = volume / 100f;
    }

    /// <summary>Cambia a la canción indicada; si ya está sonando no la reinicia.</summary>
    public void Play(string track)
    {
        if (track == _currentTrack) return;

        Stop();
        _currentTrack = track;
        _musicPath = Path.Combine(AudioFolder, track);

        // Si el sistema no tiene cómo reproducir audio el juego sigue sin música.
        try
        {
            if (OperatingSystem.IsWindows())
            {
                StartWindows();
            }
            else if (OperatingSystem.IsMacOS())
            {
                StartMac();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"No se pudo reproducir la música: {ex.Message}");
            Stop();
        }
    }

    [SupportedOSPlatform("windows")]
    private void StartWindows()
    {
        _reader = new MediaFoundationReader(_musicPath);
        var volume = new VolumeSampleProvider(new LoopSampleProvider(_reader)) { Volume = _volume };

        _output = new WasapiPlayerBuilder().WithSharedMode().Build();
        _output.Init(volume.ToWaveProvider());
        _output.Play();
    }

    [SupportedOSPlatform("windows")]
    private void StopWindows()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;

        _reader?.Dispose();
        _reader = null;
    }

    private void StartMac()
    {
        var info = new ProcessStartInfo("afplay")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add("-v");
        info.ArgumentList.Add(_volume.ToString(CultureInfo.InvariantCulture));
        info.ArgumentList.Add(_musicPath);

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };

        // afplay termina al acabar la canción: se vuelve a lanzar para mantener el loop,
        // salvo que mientras tanto se haya detenido o cambiado de canción.
        process.Exited += (_, _) =>
        {
            process.Dispose();
            if (_afplay == process)
            {
                StartMac();
            }
        };

        _afplay = process;
        process.Start();
    }

    private void Stop()
    {
        _currentTrack = null;

        if (OperatingSystem.IsWindows())
        {
            StopWindows();
        }

        var afplay = _afplay;
        _afplay = null;
        try
        {
            afplay?.Kill();
        }
        catch (Exception)
        {
            // El proceso ya había terminado.
        }
    }

    public void Dispose() => Stop();

    /// <summary>Regresa al inicio del archivo cada vez que se acaba, para que la música no se detenga.</summary>
    private sealed class LoopSampleProvider : ISampleProvider
    {
        private readonly WaveStream _source;
        private readonly ISampleProvider _samples;

        public LoopSampleProvider(WaveStream source)
        {
            _source = source;
            _samples = source.ToSampleProvider();
        }

        public WaveFormat WaveFormat => _samples.WaveFormat;

        public int Read(Span<float> buffer)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var read = _samples.Read(buffer[total..]);
                if (read == 0)
                {
                    // Si el archivo está vacío no hay nada que repetir.
                    if (_source.Position == 0) break;
                    _source.Position = 0;
                }

                total += read;
            }

            return total;
        }
    }
}
