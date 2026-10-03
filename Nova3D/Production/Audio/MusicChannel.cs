using Microsoft.Xna.Framework.Media;

namespace Nova3D.Production.Audio;

/// <summary>
/// Exclusive controller for MonoGame's process-global MediaPlayer. Songs remain
/// owned by ContentManager.
/// </summary>
public sealed class MusicChannel : IDisposable
{
    private readonly AudioBus _bus;
    private float _fadeGain = 1f;
    private float _fadeStart = 1f;
    private float _fadeTarget = 1f;
    private float _fadeDuration;
    private float _fadeElapsed;
    private bool _stopAfterFade;
    private bool _pausedByLifecycle;
    private bool _disposed;

    internal MusicChannel(AudioBus bus) => _bus = bus;

    public Song? CurrentSong { get; private set; }
    public bool IsPlaying => CurrentSong is not null && MediaPlayer.State != MediaState.Stopped;

    public void Play(Song song, bool loop = true, TimeSpan fadeIn = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(song);
        if (fadeIn < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(fadeIn));

        CurrentSong = song;
        MediaPlayer.IsRepeating = loop;
        _stopAfterFade = false;
        _pausedByLifecycle = false;
        _fadeGain = fadeIn == TimeSpan.Zero ? 1f : 0f;
        StartFade(1f, fadeIn);
        ApplyVolume();
        MediaPlayer.Play(song);
    }

    public void Stop(TimeSpan fadeOut = default)
    {
        ThrowIfDisposed();
        if (fadeOut < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(fadeOut));
        if (CurrentSong is null) return;

        if (fadeOut == TimeSpan.Zero)
        {
            StopImmediately();
            return;
        }

        _stopAfterFade = true;
        StartFade(0f, fadeOut);
    }

    public void Pause()
    {
        ThrowIfDisposed();
        if (MediaPlayer.State == MediaState.Playing) MediaPlayer.Pause();
    }

    public void Resume()
    {
        ThrowIfDisposed();
        if (MediaPlayer.State == MediaState.Paused) MediaPlayer.Resume();
    }

    internal void Update(float elapsedSeconds)
    {
        if (CurrentSong is null) return;

        if (_fadeDuration > 0f)
        {
            _fadeElapsed = MathF.Min(_fadeElapsed + elapsedSeconds, _fadeDuration);
            _fadeGain = _fadeStart + ((_fadeTarget - _fadeStart) * (_fadeElapsed / _fadeDuration));
            if (_fadeElapsed >= _fadeDuration)
            {
                _fadeGain = _fadeTarget;
                _fadeDuration = 0f;
                if (_stopAfterFade)
                {
                    StopImmediately();
                    return;
                }
            }
        }
        ApplyVolume();
    }

    internal void SetActive(bool active)
    {
        if (CurrentSong is null) return;

        if (!active && MediaPlayer.State == MediaState.Playing)
        {
            MediaPlayer.Pause();
            _pausedByLifecycle = true;
        }
        else if (active && _pausedByLifecycle && MediaPlayer.State == MediaState.Paused)
        {
            MediaPlayer.Resume();
            _pausedByLifecycle = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopImmediately();
        _disposed = true;
    }

    private void StartFade(float target, TimeSpan duration)
    {
        _fadeStart = _fadeGain;
        _fadeTarget = target;
        _fadeDuration = (float)duration.TotalSeconds;
        _fadeElapsed = 0f;
        if (_fadeDuration == 0f) _fadeGain = target;
    }

    private void ApplyVolume() => MediaPlayer.Volume = _bus.EffectiveVolume * _fadeGain;

    private void StopImmediately()
    {
        if (CurrentSong is not null) MediaPlayer.Stop();
        CurrentSong = null;
        _stopAfterFade = false;
        _fadeDuration = 0f;
        _fadeGain = 1f;
        _pausedByLifecycle = false;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
