using System.Diagnostics;
using Microsoft.Xna.Framework.Audio;
using Gasci.Core;

namespace Gasci.Audio;

/// <summary>
/// Three sound categories (see gdd.md):
///  - Music: long looping tracks (or ambiences like wind).
///  - Sound effects: short sounds for actions.
///  - Cursor: the tick played every time a cursor/text box writes a character, with random pitch.
/// A file in assets/audio/music/&lt;id&gt;.wav or assets/audio/sfx/&lt;id&gt;.wav replaces the synthesized
/// placeholder with the same id. If there is no audio device the service stays silent.
/// </summary>
public sealed class AudioService : IDisposable
{
    private static readonly TimeSpan CursorThrottle = TimeSpan.FromMilliseconds(35);

    private readonly Dictionary<string, SoundEffect?> _sfx = new();
    private readonly Dictionary<string, SoundEffect?> _music = new();
    private readonly Stopwatch _sinceCursor = Stopwatch.StartNew();
    private readonly bool _enabled;
    private SoundEffectInstance? _musicInstance;

    public string? CurrentMusic { get; private set; }

    public AudioService()
    {
        try
        {
            SoundEffect.MasterVolume = 1f;
            _enabled = true;
        }
        catch (Exception e)
        {
            Log.Warn($"Audio disabled: {e.Message}");
        }
    }

    private float _musicVolume = 0.6f;

    /// <summary>0..1, set by the settings variable bound to "audio.music".</summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, 0f, 1f);
            if (_musicInstance is not null) _musicInstance.Volume = _musicVolume;
        }
    }

    /// <summary>0..1, set by the settings variable bound to "audio.sfx".</summary>
    public float SfxVolume { get; set; } = 0.7f;

    public void PlaySfx(string id, float volume = 1f, float pitchVariance = 0f)
    {
        if (!_enabled || SfxVolume <= 0f) return;
        SoundEffect? effect = Get(_sfx, "sfx", id, Synth.Sfx);
        if (effect is null) return;

        float pitch = pitchVariance > 0 ? (float)(GameServices.Rng.NextDouble() * 2 - 1) * pitchVariance : 0f;
        try { effect.Play(Math.Clamp(volume * SfxVolume, 0f, 1f), pitch, 0f); }
        catch (Exception e) { Debug.WriteLine(e); }
    }

    /// <summary>
    /// Cursor tick with random pitch. Cursors can write hundreds of characters per second, so the
    /// tick is throttled: otherwise it would become noise (and exhaust the audio voices).
    /// </summary>
    public void PlayCursor()
    {
        if (_sinceCursor.Elapsed < CursorThrottle) return;
        _sinceCursor.Restart();
        PlaySfx("cursor", 0.35f, 0.6f);
    }

    /// <summary>
    /// Plays a looping track; the same id keeps playing, null or "" stops the music. The service does
    /// not know who asked: maps, scenes and actions all call it the same way.
    /// </summary>
    public void PlayMusic(string? id)
    {
        if (string.IsNullOrEmpty(id)) id = null;
        if (id == CurrentMusic) return;
        StopMusic();
        CurrentMusic = id;
        if (!_enabled || string.IsNullOrEmpty(id)) return;

        SoundEffect? track = Get(_music, "music", id, Synth.Music);
        if (track is null) return;
        try
        {
            _musicInstance = track.CreateInstance();
            _musicInstance.IsLooped = true;
            _musicInstance.Volume = MusicVolume;
            _musicInstance.Play();
        }
        catch (Exception e) { Debug.WriteLine(e); }
    }

    public void StopMusic()
    {
        _musicInstance?.Stop();
        _musicInstance?.Dispose();
        _musicInstance = null;
        CurrentMusic = null;
    }

    private static SoundEffect? Get(Dictionary<string, SoundEffect?> cache, string folder, string id,
        Func<string, SoundEffect?> synthesize)
    {
        if (cache.TryGetValue(id, out SoundEffect? effect)) return effect;
        string file = Path.Combine(Paths.Audio, folder, id + ".wav");
        try
        {
            effect = File.Exists(file) ? SoundEffect.FromFile(file) : synthesize(id);
        }
        catch (Exception e)
        {
            Log.Warn($"Could not load sound '{id}': {e.Message}");
            effect = null;
        }
        cache[id] = effect;
        return effect;
    }

    public void Dispose()
    {
        StopMusic();
        foreach (SoundEffect? s in _sfx.Values.Concat(_music.Values)) s?.Dispose();
    }
}
