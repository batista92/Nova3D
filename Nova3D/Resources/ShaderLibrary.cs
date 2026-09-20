using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Resources;

/// <summary>
/// Named access to compiled effects. ContentManager remains the owner of the
/// resources; the library adds project-level names and future reload hooks.
/// </summary>
public sealed class ShaderLibrary
{
    private readonly ResourceLibrary _resources;
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    public ShaderLibrary(ResourceLibrary resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public int Count => _names.Count;

    public Effect Load(string name, string assetName)
    {
        var effect = _resources.Load<Effect>(name, assetName);
        _names.Add(name);
        return effect;
    }

    public Effect Get(string name)
    {
        if (!_names.Contains(name))
            throw new KeyNotFoundException($"Shader '{name}' is not registered.");
        return _resources.Get<Effect>(name);
    }

    public bool TryGet(string name, out Effect? effect)
    {
        if (_names.Contains(name) && _resources.TryGet(name, out effect)) return true;
        effect = null;
        return false;
    }

    public void Clear()
    {
        foreach (var name in _names)
            _resources.Remove<Effect>(name);
        _names.Clear();
    }
}
