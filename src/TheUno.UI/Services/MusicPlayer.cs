using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TheUno.UI.Services;

/// <summary>
/// Reproduce la música de fondo en loop durante toda la aplicación (menú y partida).
/// En Windows usa NAudio y en macOS el reproductor afplay que trae el sistema.
/// </summary>
public sealed class MusicPlayer : IDisposable
{
    private static readonly string MusicPath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "musica_8bit.mp3");

    private float _volume;
    private bool _stopped;

    // Windows
    private MediaFoundationReader? _reader;
    private WasapiPlayer? _output;

    // macOS
    private Process? _afplay;

    public void Start(int volume = 60)
    {
        _volume = volume / 100f;
        _stopped = false;

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
            Dispose();
        }
    }

    [SupportedOSPlatform("windows")]
    private void StartWindows()
    {
        _reader = new MediaFoundationReader(MusicPath);
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
        info.ArgumentList.Add(MusicPath);

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };

        // afplay termina al acabar la canción: se vuelve a lanzar para mantener el loop.
        process.Exited += (_, _) =>
        {
            process.Dispose();
            if (!_stopped)
            {
                StartMac();
            }
        };

        _afplay = process;
        process.Start();
    }

    public void Dispose()
    {
        _stopped = true;

        if (OperatingSystem.IsWindows())
        {
            StopWindows();
        }

        try
        {
            _afplay?.Kill();
        }
        catch (Exception)
        {
            // El proceso ya había terminado.
        }

        _afplay = null;
    }

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
