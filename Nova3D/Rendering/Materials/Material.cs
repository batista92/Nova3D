using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering.Materials;

/// <summary>
/// A renderer-level material. The Effect remains a MonoGame object and is
/// owned by the ContentManager; a material only supplies render semantics.
/// </summary>
public abstract class Material
{
    protected Material(string name, Effect effect, string? technique = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        Technique = technique;
    }

    public string Name { get; }
    public Effect Effect { get; }
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
