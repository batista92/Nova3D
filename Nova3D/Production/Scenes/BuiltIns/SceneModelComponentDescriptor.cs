using Nova3D.Production.Assets.Gltf;
using Nova3D.Production.Scenes.Assets;
using Nova3D.Rendering.Models;

namespace Nova3D.Production.Scenes.BuiltIns;

/// <summary>Creates a renderable GLB/glTF component through explicit asset dependencies.</summary>
public sealed class SceneModelComponentDescriptor : ISceneRuntimeComponentDescriptor
{
    private readonly Func<SceneComponentInstantiationContext, string, SceneModelAsset> _loadModel;
    private readonly Func<GltfModel, GltfModelRenderer> _createRenderer;

    public SceneModelComponentDescriptor(
        Func<SceneComponentInstantiationContext, string, GltfModel> loadModel,
        Func<GltfModel, GltfModelRenderer> createRenderer,
        bool ownsModels = true)
    {
        ArgumentNullException.ThrowIfNull(loadModel);
        _loadModel = (context, asset) => ownsModels
            ? SceneModelAsset.Owned(loadModel(context, asset))
            : SceneModelAsset.Borrowed(loadModel(context, asset));
        _createRenderer = createRenderer ?? throw new ArgumentNullException(nameof(createRenderer));
    }

    public SceneModelComponentDescriptor(
        Func<SceneComponentInstantiationContext, string, SceneModelAsset> loadModel,
        Func<GltfModel, GltfModelRenderer> createRenderer)
    {
        _loadModel = loadModel ?? throw new ArgumentNullException(nameof(loadModel));
        _createRenderer = createRenderer ?? throw new ArgumentNullException(nameof(createRenderer));
    }

    public const string ComponentType = "nova3d.model";
    public string Type => ComponentType;

    public void Validate(SceneComponentValidationContext context)
    {
        SceneBuiltInProperties.ValidateKnown(context, "asset", "visible", "castShadows");
        if (SceneBuiltInProperties.RequireString(context, "asset", out var asset))
            ValidateAsset(context, asset);
        SceneBuiltInProperties.OptionalBoolean(context, "visible", true);
        SceneBuiltInProperties.OptionalBoolean(context, "castShadows", true);
    }

    public object Create(SceneComponentInstantiationContext context)
    {
        var asset = SceneBuiltInProperties.ReadString(context.Component, "asset");
        var modelAsset = _loadModel(context, asset) ??
            throw new InvalidOperationException($"Model loader returned null for '{asset}'.");
        GltfModelRenderer? renderer = null;
        try
        {
            renderer = _createRenderer(modelAsset.Model) ??
                throw new InvalidOperationException($"Renderer factory returned null for '{asset}'.");
            renderer.Transform = context.WorldTransform;
            return new SceneModelComponent(
                asset,
                modelAsset,
                renderer,
                SceneBuiltInProperties.ReadBoolean(context.Component, "visible", true),
                SceneBuiltInProperties.ReadBoolean(context.Component, "castShadows", true));
        }
        catch
        {
            renderer?.Dispose();
            modelAsset.Dispose();
            throw;
        }
    }

    public static SceneModelComponentDescriptor CreateCached(
        SceneAssetResolver resolver,
        SceneAssetCache<GltfModel> cache,
        Func<GltfModel, GltfModelRenderer> createRenderer)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(cache);
        return new SceneModelComponentDescriptor(
            (context, asset) =>
            {
                var path = resolver.ResolveExistingFile(context.Plan, asset);
                return SceneModelAsset.Leased(cache.Acquire(path, context.CancellationToken));
            },
            createRenderer);
    }

    public void Destroy(object instance)
    {
        if (instance is not SceneModelComponent model)
            throw new ArgumentException("Expected a SceneModelComponent instance.", nameof(instance));
        model.Dispose();
    }

    private static void ValidateAsset(SceneComponentValidationContext context, string asset)
    {
        var invalid = asset.Contains('\\') ||
                      Path.IsPathRooted(asset) ||
                      Uri.TryCreate(asset, UriKind.Absolute, out _) ||
                      asset.Split('/').Any(segment => segment is "" or "." or "..");
        if (invalid)
        {
            context.Report(SceneBuiltInValidationCodes.InvalidPropertyValue,
                "Asset must be a portable path relative to the configured Assets root.", "asset");
            return;
        }

        var extension = Path.GetExtension(asset);
        if (!string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase))
        {
            context.Report(SceneBuiltInValidationCodes.InvalidPropertyValue,
                "Model asset must use the .glb or .gltf extension.", "asset");
        }
    }
}
