namespace Nova3D.Production.Audio;

/// <summary>
/// A named volume category with optional parent gain and allocation-free linear fades.
/// </summary>
public sealed class AudioBus
{
    private float _volume = 1f;
    private float _fadeStart = 1f;
    private float _fadeTarget = 1f;
    private float _fadeDuration;
    private float _fadeElapsed;

    internal AudioBus(string name, AudioBus? parent)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Audio bus name cannot be empty.", nameof(name));

        Name = name;
        Parent = parent;
    }

    public string Name { get; }
    public AudioBus? Parent { get; }

    public float Volume
    {
        get => _volume;
        set
        {
            ValidateVolume(value, nameof(value));
            _volume = value;
            _fadeDuration = 0f;
            _fadeElapsed = 0f;
            _fadeStart = value;
            _fadeTarget = value;
        }
    }

    public bool IsMuted { get; set; }
    public bool IsFading => _fadeDuration > 0f;
    public float EffectiveVolume => IsMuted ? 0f : _volume * (Parent?.EffectiveVolume ?? 1f);

    public void FadeTo(float volume, TimeSpan duration)
    {
        ValidateVolume(volume, nameof(volume));
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Fade duration cannot be negative.");

        float seconds = (float)duration.TotalSeconds;
        if (seconds == 0f)
        {
            Volume = volume;
            return;
        }

        _fadeStart = _volume;
        _fadeTarget = volume;
        _fadeDuration = seconds;
        _fadeElapsed = 0f;
    }

    internal void Update(float elapsedSeconds)
    {
        if (_fadeDuration <= 0f) return;

        _fadeElapsed = MathF.Min(_fadeElapsed + elapsedSeconds, _fadeDuration);
        float amount = _fadeElapsed / _fadeDuration;
        _volume = _fadeStart + ((_fadeTarget - _fadeStart) * amount);
        if (_fadeElapsed >= _fadeDuration)
        {
            _volume = _fadeTarget;
            _fadeDuration = 0f;
        }
    }

    internal static void ValidateVolume(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0f || value > 1f)
            throw new ArgumentOutOfRangeException(parameterName, "Volume must be finite and between 0 and 1.");
    }
}
