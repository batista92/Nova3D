using Microsoft.Xna.Framework.Audio;

namespace Nova3D.Production.Audio;

/// <summary>
/// Game-owned coordinator for audio buses, music and bounded SFX pools.
/// </summary>
public sealed class AudioSystem : IDisposable
{
    private readonly List<SoundEffectPool> _pools = [];
    private bool _isActive = true;
    private bool _disposed;

    public AudioSystem()
    {
        Mixer = new AudioMixer();
        Music = new MusicChannel(Mixer.Music);
    }

    public AudioMixer Mixer { get; }
    public MusicChannel Music { get; }
    public bool IsActive => _isActive;

    public SoundEffectPool CreateSoundPool(
        SoundEffect soundEffect,
        int capacity = 8,
        AudioBus? bus = null,
        AudioPoolOverflowPolicy overflowPolicy = AudioPoolOverflowPolicy.Reject)
    {
        ThrowIfDisposed();
        var pool = new SoundEffectPool(soundEffect, bus ?? Mixer.Sfx, capacity, overflowPolicy);
        pool.SetActive(_isActive);
        _pools.Add(pool);
        return pool;
    }

    public void Update(TimeSpan elapsed)
    {
        ThrowIfDisposed();
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        float seconds = (float)elapsed.TotalSeconds;
        Mixer.Update(seconds);
        Music.Update(seconds);
        for (int index = 0; index < _pools.Count; index++) _pools[index].Update();
    }

    /// <summary>Pause/resume only playback that this system paused due to focus loss.</summary>
    public void SetActive(bool active)
    {
        ThrowIfDisposed();
        if (_isActive == active) return;
        _isActive = active;
        Music.SetActive(active);
        for (int index = 0; index < _pools.Count; index++) _pools[index].SetActive(active);
    }

    public void Dispose()
    {
        if (_disposed) return;
        for (int index = _pools.Count - 1; index >= 0; index--) _pools[index].Dispose();
        _pools.Clear();
        Music.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
