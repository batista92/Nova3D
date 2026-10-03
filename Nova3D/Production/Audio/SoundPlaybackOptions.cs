namespace Nova3D.Production.Audio;

public enum AudioPoolOverflowPolicy
{
    Reject,
    StealOldest
}

/// <summary>Native MonoGame playback values plus loop intent.</summary>
public readonly record struct SoundPlaybackOptions(
    float Volume = 1f,
    float Pitch = 0f,
    float Pan = 0f,
    bool IsLooped = false)
{
    internal void Validate()
    {
        AudioBus.ValidateVolume(Volume, nameof(Volume));
        if (!float.IsFinite(Pitch) || Pitch < -1f || Pitch > 1f)
            throw new ArgumentOutOfRangeException(nameof(Pitch), "Pitch must be between -1 and 1.");
        if (!float.IsFinite(Pan) || Pan < -1f || Pan > 1f)
            throw new ArgumentOutOfRangeException(nameof(Pan), "Pan must be between -1 and 1.");
    }
}
