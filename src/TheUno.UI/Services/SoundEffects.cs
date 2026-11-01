using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TheUno.UI.Services;

/// <summary>
/// Efectos de sonido cortos (picar un botón, tirar o robar una carta). Pueden sonar varios a la vez
/// y encima de la música. En Windows usa NAudio y en macOS el reproductor afplay que trae el sistema.
/// </summary>
public sealed class SoundEffects : IDisposable
{
    public const string ButtonClick = "button_click.mp3";
    public const string CardSound = "card_sound.mp3";

    private static readonly string AudioFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");

    // Ganancia extra por sonido, para emparejarlos entre sí (el volumen general va en el constructor).
    // Con ganancia, los picos se redondean (ver Load) para que no truenen.
    private static readonly Dictionary<string, float> Gains = new()
    {
        [CardSound] = 2f,
        [ButtonClick] = 2f
    };

    // Varias cartas al mismo tiempo (un +4) se oyen como una sola, si no se suman y truenan
    private static readonly TimeSpan RepeatGap = TimeSpan.FromMilliseconds(80);
    private readonly Dictionary<string, DateTime> _lastPlayed = new();

    // Formato de la mezcla: todos los sonidos se convierten a este al cargarse
    private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    private readonly float _volume;

    // Windows: cada sonido ya decodificado en memoria, para que suene al instante.
    // La salida queda abierta todo el tiempo y el mezclador suma los sonidos que estén tocando.
    private readonly Dictionary<string, float[]> _samples = new();
    private MixingSampleProvider? _mixer;
    private WasapiPlayer? _output;
    private bool _failed;

    public SoundEffects(int volume = 25)
    {
        _volume = volume / 100f;
    }

    /// <summary>Toca el sonido una vez. Si el sistema no puede reproducir audio, no pasa nada.</summary>
    public void Play(string sound)
    {
        if (_failed) return;

        var now = DateTime.UtcNow;
        if (_lastPlayed.TryGetValue(sound, out var last) && now - last < RepeatGap) return;
        _lastPlayed[sound] = now;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                PlayWindows(sound);
            }
            else if (OperatingSystem.IsMacOS())
            {
                PlayMac(sound);
            }
        }
        catch (Exception ex)
        {
            // Sin audio el juego sigue igual; no se vuelve a intentar para no trabar cada clic
            Debug.WriteLine($"No se pudo reproducir el sonido: {ex.Message}");
            _failed = true;
        }
    }

    [SupportedOSPlatform("windows")]
    private void PlayWindows(string sound)
    {
        if (_mixer is null)
        {
            _mixer = new MixingSampleProvider(MixFormat) { ReadFully = true };
            _output = new WasapiPlayerBuilder().WithSharedMode().Build();
            _output.Init(_mixer.ToWaveProvider());
            _output.Play();
        }

        if (!_samples.TryGetValue(sound, out var samples))
        {
            samples = Load(sound);
            _samples[sound] = samples;
        }

        _mixer.AddMixerInput(new MemorySampleProvider(samples));
    }

    /// <summary>
    /// Decodifica el archivo completo al formato de la mezcla, con el volumen ya aplicado.
    /// Con ganancia extra, los picos se redondean (Tanh) en lugar de cortarse en seco.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private float[] Load(string sound)
    {
        var path = Path.Combine(AudioFolder, sound);
        var gain = Gains.GetValueOrDefault(sound, 1f);
        using var reader = new MediaFoundationReader(path);
        ISampleProvider provider = reader.ToSampleProvider();

        if (provider.WaveFormat.Channels == 1) provider = new MonoToStereoSampleProvider(provider);
        if (provider.WaveFormat.SampleRate != MixFormat.SampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, MixFormat.SampleRate);
        }

        var samples = new List<float>();
        var buffer = new float[MixFormat.SampleRate * MixFormat.Channels];
        int read;
        while ((read = provider.Read(buffer)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var sample = buffer[i] * gain;
                if (gain > 1f) sample = MathF.Tanh(sample);
                samples.Add(sample * _volume);
            }
        }

        return samples.ToArray();
    }

    // En macOS cada sonido es un afplay aparte, así pueden sonar varios a la vez
    private void PlayMac(string sound)
    {
        var info = new ProcessStartInfo("afplay")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // afplay acepta volumen arriba de 1 para subirlo
        var volume = _volume * Gains.GetValueOrDefault(sound, 1f);
        info.ArgumentList.Add("-v");
        info.ArgumentList.Add(volume.ToString(CultureInfo.InvariantCulture));
        info.ArgumentList.Add(Path.Combine(AudioFolder, sound));

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.Exited += (_, _) => process.Dispose();
        process.Start();
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            _output?.Stop();
            _output?.Dispose();
        }

        _output = null;
        _mixer = null;
    }

    /// <summary>Lee un sonido ya cargado de principio a fin; al terminar, el mezclador lo quita solo.</summary>
    private sealed class MemorySampleProvider : ISampleProvider
    {
        private readonly float[] _samples;
        private int _position;

        public MemorySampleProvider(float[] samples)
        {
            _samples = samples;
        }

        public WaveFormat WaveFormat => MixFormat;

        public int Read(Span<float> buffer)
        {
            var count = Math.Min(buffer.Length, _samples.Length - _position);
            _samples.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }
    }
}
