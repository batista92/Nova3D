namespace Nova3D.Production.Audio;

/// <summary>Owns the master, music, SFX and game-defined audio buses.</summary>
public sealed class AudioMixer
{
    private readonly List<AudioBus> _buses = [];
    private readonly Dictionary<string, AudioBus> _busesByName =
        new(StringComparer.OrdinalIgnoreCase);

    public AudioMixer()
    {
        Master = Add("Master", null);
        Music = Add("Music", Master);
        Sfx = Add("SFX", Master);
    }

    public AudioBus Master { get; }
    public AudioBus Music { get; }
    public AudioBus Sfx { get; }
    public IReadOnlyList<AudioBus> Buses => _buses;

    public AudioBus CreateBus(string name, AudioBus? parent = null)
    {
        if (_busesByName.ContainsKey(name))
            throw new InvalidOperationException($"An audio bus named '{name}' already exists.");

        if (parent is not null && !_buses.Contains(parent))
            throw new ArgumentException("The parent bus must belong to this mixer.", nameof(parent));

        return Add(name, parent ?? Master);
    }

    public bool TryGetBus(string name, out AudioBus bus) =>
        _busesByName.TryGetValue(name, out bus!);

    internal void Update(float elapsedSeconds)
    {
        for (int index = 0; index < _buses.Count; index++)
            _buses[index].Update(elapsedSeconds);
    }

    private AudioBus Add(string name, AudioBus? parent)
    {
        var bus = new AudioBus(name, parent);
        _buses.Add(bus);
        _busesByName.Add(name, bus);
        return bus;
    }
}
