using Microsoft.Xna.Framework.Audio;

namespace Nova3D.Production.Audio;

/// <summary>
/// Bounded owner of reusable <see cref="SoundEffectInstance"/> objects created
/// from one ContentManager-owned <see cref="SoundEffect"/>.
/// </summary>
public sealed class SoundEffectPool : IDisposable
{
    private readonly Voice[] _voices;
    private long _playSequence;
    private bool _isActive = true;
    private bool _disposed;

    public SoundEffectPool(
        SoundEffect soundEffect,
        AudioBus bus,
        int capacity = 8,
        AudioPoolOverflowPolicy overflowPolicy = AudioPoolOverflowPolicy.Reject)
    {
        ArgumentNullException.ThrowIfNull(soundEffect);
        ArgumentNullException.ThrowIfNull(bus);
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

        SoundEffect = soundEffect;
        Bus = bus;
        OverflowPolicy = overflowPolicy;
        _voices = new Voice[capacity];
        for (int index = 0; index < capacity; index++)
            _voices[index] = new Voice(soundEffect.CreateInstance());
    }

    public SoundEffect SoundEffect { get; }
    public AudioBus Bus { get; }
    public AudioPoolOverflowPolicy OverflowPolicy { get; }
    public int Capacity => _voices.Length;

    public int ActiveCount
    {
        get
        {
            ThrowIfDisposed();
            int count = 0;
            for (int index = 0; index < _voices.Length; index++)
                if (_voices[index].Instance.State != SoundState.Stopped) count++;
            return count;
        }
    }

    public SoundEffectInstance? Play() => Play(new SoundPlaybackOptions());

    public SoundEffectInstance? Play(SoundPlaybackOptions options) =>
        PlayCore(options, null, null);

    public SoundEffectInstance? Play3D(AudioListener listener, AudioEmitter emitter) =>
        Play3D(listener, emitter, new SoundPlaybackOptions());

    public SoundEffectInstance? Play3D(
        AudioListener listener,
        AudioEmitter emitter,
        SoundPlaybackOptions options)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(emitter);
        return PlayCore(options, listener, emitter);
    }

    public void Update()
    {
        ThrowIfDisposed();
        for (int index = 0; index < _voices.Length; index++)
        {
            Voice voice = _voices[index];
            if (voice.Instance.State == SoundState.Stopped) continue;
            ApplyVolumeAndSpatialization(voice);
        }
    }

    public void StopAll(bool immediate = true)
    {
        ThrowIfDisposed();
        for (int index = 0; index < _voices.Length; index++)
        {
            _voices[index].Instance.Stop(immediate);
            _voices[index].PausedByLifecycle = false;
        }
    }

    internal void SetActive(bool active)
    {
        ThrowIfDisposed();
        if (_isActive == active) return;
        _isActive = active;

        for (int index = 0; index < _voices.Length; index++)
        {
            Voice voice = _voices[index];
            if (!active && voice.Instance.State == SoundState.Playing)
            {
                voice.Instance.Pause();
                voice.PausedByLifecycle = true;
            }
            else if (active && voice.PausedByLifecycle && voice.Instance.State == SoundState.Paused)
            {
                voice.Instance.Resume();
                voice.PausedByLifecycle = false;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        for (int index = 0; index < _voices.Length; index++)
        {
            _voices[index].Instance.Stop(true);
            _voices[index].Instance.Dispose();
        }
        _disposed = true;
    }

    private SoundEffectInstance? PlayCore(
        SoundPlaybackOptions options,
        AudioListener? listener,
        AudioEmitter? emitter)
    {
        ThrowIfDisposed();
        options.Validate();
        if (!_isActive) return null;

        Voice? voice = null;
        for (int index = 0; index < _voices.Length; index++)
        {
            if (_voices[index].Instance.State == SoundState.Stopped)
            {
                voice = _voices[index];
                break;
            }
        }

        if (voice is null && OverflowPolicy == AudioPoolOverflowPolicy.StealOldest)
        {
            voice = _voices[0];
            for (int index = 1; index < _voices.Length; index++)
                if (_voices[index].Sequence < voice.Sequence) voice = _voices[index];
            voice.Instance.Stop(true);
        }

        if (voice is null) return null;

        voice.BaseVolume = options.Volume;
        voice.Listener = listener;
        voice.Emitter = emitter;
        voice.Sequence = ++_playSequence;
        voice.PausedByLifecycle = false;
        voice.Instance.Pitch = options.Pitch;
        voice.Instance.Pan = options.Pan;
        voice.Instance.IsLooped = options.IsLooped;
        ApplyVolumeAndSpatialization(voice);
        voice.Instance.Play();
        return voice.Instance;
    }

    private void ApplyVolumeAndSpatialization(Voice voice)
    {
        voice.Instance.Volume = voice.BaseVolume * Bus.EffectiveVolume;
        if (voice.Listener is not null && voice.Emitter is not null)
            voice.Instance.Apply3D(voice.Listener, voice.Emitter);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class Voice(SoundEffectInstance instance)
    {
        public SoundEffectInstance Instance { get; } = instance;
        public float BaseVolume { get; set; }
        public long Sequence { get; set; }
        public bool PausedByLifecycle { get; set; }
        public AudioListener? Listener { get; set; }
        public AudioEmitter? Emitter { get; set; }
    }
}
