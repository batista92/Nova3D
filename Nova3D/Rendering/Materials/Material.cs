using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering.Materials;

/// <summary>
/// A renderer-level material. The Effect remains a MonoGame object and is
/// owned by the ContentManager; a material only supplies render semantics.
/// </summary>
public abstract class Material
{
    private readonly Func<Effect> _effectProvider;

    protected Material(string name, Effect effect, string? technique = null)
        : this(name, () => effect, technique)
    {
        ArgumentNullException.ThrowIfNull(effect);
    }

    protected Material(string name, Func<Effect> effectProvider, string? technique = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _effectProvider = effectProvider ?? throw new ArgumentNullException(nameof(effectProvider));
        _ = Effect;
        Technique = technique;
    }

    public string Name { get; }
    public Effect Effect => _effectProvider() ?? throw new InvalidOperationException(
        $"Effect provider for material '{Name}' returned null.");
    public string? Technique { get; }

    public void Apply(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Technique is not null)
            Effect.CurrentTechnique = Effect.Techniques[Technique];
        ApplyParameters(context);
    }

    protected abstract void ApplyParameters(RenderContext context);
}
