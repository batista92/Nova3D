using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Production.Assets.Gltf;
using Nova3D.Production.Logging;
using Nova3D.Rendering;
using Nova3D.Rendering.Lighting;
using Nova3D.Rendering.Models;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

namespace CityBuilder.Benchmarks.CityBenchmark;

internal sealed class GltfValidationGallery : IDisposable
{
    private sealed record Entry(GltfModel Model, GltfModelRenderer Renderer);
    private readonly List<Entry> _entries = new();
    private readonly ILogger _logger;

    public GltfValidationGallery(GraphicsDevice device, Effect pbrEffect, NovaDirectionalLight light,
        ImageBasedLighting environment, string directory, ILogger logger)
    {
        _logger = logger;
        if (!Directory.Exists(directory))
        {
            _logger.Log(LogLevel.Warning, "glTF", $"Validation directory not found: {directory}");
            return;
        }

        var importer = new GltfImporter(device);
        var files = Directory.EnumerateFiles(directory, "*.glb", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        const int columns = 7;
        const float spacing = 105f;
        for (var index = 0; index < files.Length; index++)
        {
            try
            {
                var model = importer.Load(files[index]);
                var renderer = new GltfModelRenderer(model, pbrEffect, light, environment);
                var bounds = model.Bounds;
                var size = bounds.Max - bounds.Min;
                var largest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
                var scale = largest > 0.0001f ? 62f / largest : 1f;
                var x = (index % columns - (columns - 1) * 0.5f) * spacing;
                var z = -(index / columns) * spacing + 210f;
                var ground = LargeWorldTerrain.SampleHeight(x, z);
                var centerX = (bounds.Min.X + bounds.Max.X) * 0.5f;
                var centerZ = (bounds.Min.Z + bounds.Max.Z) * 0.5f;
                renderer.Transform = Matrix.CreateTranslation(-centerX, -bounds.Min.Y, -centerZ) *
                                     Matrix.CreateScale(scale) *
                                     Matrix.CreateTranslation(x, ground + 1f, z);
                _entries.Add(new Entry(model, renderer));
            }
            catch (Exception exception)
            {
                FailedCount++;
                _logger.Log(LogLevel.Error, "glTF", $"Failed to load '{files[index]}'.", exception);
            }
        }
        FileCount = files.Length;
        _logger.Log(LogLevel.Information, "glTF",
            $"Validation gallery loaded {_entries.Count}/{FileCount} GLB files; {FailedCount} failed.");
    }

    public int FileCount { get; }
    public int LoadedCount => _entries.Count;
    public int FailedCount { get; private set; }

    public void Draw(RenderContext context)
    {
        foreach (var entry in _entries) entry.Renderer.Draw(context);
    }

    public void DrawShadows(RenderContext context, Effect shadowEffect, Matrix lightViewProjection)
    {
        foreach (var entry in _entries)
            entry.Renderer.DrawShadows(context, shadowEffect, lightViewProjection);
    }

    public void Dispose()
    {
        foreach (var entry in _entries)
        {
            entry.Renderer.Dispose();
            entry.Model.Dispose();
        }
        _entries.Clear();
    }
}
