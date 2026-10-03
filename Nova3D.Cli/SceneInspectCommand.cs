using Nova3D.Production.Assets.Gltf;
using Nova3D.Production.Scenes;
using Nova3D.Production.Scenes.Assets;
using Nova3D.Production.Scenes.BuiltIns;
using Nova3D.Production.Scenes.Prefabs;
using Nova3D.Rendering.Models;

internal static class SceneInspectCommand
{
    public static int Run(string path, TextWriter output)
    {
        SceneInspectionReport report = new(output);
        output.WriteLine("Nova3D scene inspection");
        report.Pass("file", path);

        try
        {
            SceneDocument document = SceneDocumentSerializer.Load(path);
            SceneComponentRegistry registry = CreateRegistry();
            bool hasPrefabs = document.Nodes
                .SelectMany(node => node.Components)
                .Any(component => component.Type == ScenePrefabComponentDescriptor.ComponentType);
            bool hasModels = document.Nodes
                .SelectMany(node => node.Components)
                .Any(component => component.Type == SceneModelComponentDescriptor.ComponentType);

            string? assetsRoot = null;
            SceneAssetResolver? assets = null;
            if (hasPrefabs || hasModels)
            {
                (assetsRoot, bool usedFallback) = FindAssetsRoot(path);
                assets = new SceneAssetResolver(assetsRoot);
                if (usedFallback)
                {
                    report.Warn("assets-root", $"no Assets ancestor found; using scene directory {assetsRoot}.");
                }
                else
                {
                    report.Pass("assets-root", assetsRoot);
                }
            }

            SceneLoadPlan plan = hasPrefabs
                ? ScenePrefabLoader.Prepare(document, path, registry, assets!)
                : SceneLoader.Prepare(document, registry, path);

            ValidateModelAssets(plan, assets, report);
            int components = plan.Document.Nodes.Sum(node => node.Components.Count);
            int roots = plan.Document.Nodes.Count(node => node.ParentId is null);
            int maximumDepth = CalculateMaximumDepth(plan.Document.Nodes);
            string componentTypes = string.Join(
                ", ",
                plan.Document.Nodes
                    .SelectMany(node => node.Components)
                    .GroupBy(component => component.Type, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => $"{group.Key}={group.Count()}"));

            report.Pass("document", $"{document.Format}/{document.Version} | {document.Name}");
            report.Info(
                "content",
                $"source nodes {document.Nodes.Count} | expanded nodes {plan.Nodes.Count} | roots {roots} | depth {maximumDepth} | components {components}");
            report.Info("components", componentTypes.Length == 0 ? "none" : componentTypes);
        }
        catch (SceneDocumentValidationException exception)
        {
            foreach (SceneValidationIssue issue in exception.Issues)
            {
                report.Fail(issue.Code, $"{exception.DocumentPath} {issue.JsonPath}: {issue.Message}");
            }
        }
        catch (SceneDocumentParseException exception)
        {
            report.Fail("scene-json", OneLine(exception.Message));
        }
        catch (ScenePrefabException exception)
        {
            report.Fail("prefab", OneLine(exception.Message));
        }
        catch (SceneAssetNotFoundException exception)
        {
            report.Fail("asset", $"'{exception.AssetReference}' was not found at {exception.ResolvedPath}.");
        }
        catch (SceneAssetPathException exception)
        {
            report.Fail("asset", OneLine(exception.Message));
        }
        catch (Exception exception)
        {
            report.Fail("scene", OneLine(exception.Message));
        }

        return report.Complete();
    }

    private static SceneComponentRegistry CreateRegistry()
    {
        Func<SceneComponentInstantiationContext, string, GltfModel> loadModel =
            (_, _) => throw new InvalidOperationException("Scene inspection never loads GPU models.");
        Func<GltfModel, GltfModelRenderer> createRenderer =
            _ => throw new InvalidOperationException("Scene inspection never creates renderers.");
        SceneComponentRegistry registry = new();
        registry.RegisterNova3DBuiltIns(
            new SceneModelComponentDescriptor(loadModel, createRenderer));
        return registry;
    }

    private static void ValidateModelAssets(
        SceneLoadPlan plan,
        SceneAssetResolver? assets,
        SceneInspectionReport report)
    {
        string[] references = plan.Document.Nodes
            .SelectMany(node => node.Components)
            .Where(component => component.Type == SceneModelComponentDescriptor.ComponentType)
            .Select(component => component.Properties.GetProperty("asset").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();
        if (references.Length == 0)
        {
            return;
        }

        if (assets is null)
        {
            report.Fail("asset", "model references require an Assets root.");
            return;
        }

        foreach (string reference in references)
        {
            string resolved = assets.ResolveExistingFile(plan, reference);
            report.Pass("asset", $"{reference} -> {resolved}");
        }
    }

    private static (string Root, bool UsedFallback) FindAssetsRoot(string scenePath)
    {
        DirectoryInfo? directory = new(Path.GetDirectoryName(scenePath)!);
        DirectoryInfo sceneDirectory = directory;
        while (directory is not null)
        {
            if (string.Equals(directory.Name, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return (directory.FullName, false);
            }
            directory = directory.Parent;
        }

        if (string.Equals(sceneDirectory.Name, "Prefabs", StringComparison.OrdinalIgnoreCase) &&
            sceneDirectory.Parent is not null)
        {
            return (sceneDirectory.Parent.FullName, true);
        }

        return (sceneDirectory.FullName, true);
    }

    private static int CalculateMaximumDepth(IReadOnlyList<SceneNodeDocument> nodes)
    {
        if (nodes.Count == 0)
        {
            return 0;
        }

        Dictionary<string, SceneNodeDocument> byId = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        int maximum = 0;
        foreach (SceneNodeDocument node in nodes)
        {
            int depth = 1;
            string? parent = node.ParentId;
            while (parent is not null)
            {
                depth++;
                parent = byId[parent].ParentId;
            }
            maximum = Math.Max(maximum, depth);
        }
        return maximum;
    }

    private static string OneLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}

internal sealed class SceneInspectionReport
{
    private readonly TextWriter output;
    private int warnings;
    private int errors;

    public SceneInspectionReport(TextWriter output) => this.output = output;

    public void Pass(string name, string message) => output.WriteLine($"PASS {name} | {message}");

    public void Info(string name, string message) => output.WriteLine($"INFO {name} | {message}");

    public void Warn(string name, string message)
    {
        warnings++;
        output.WriteLine($"WARN {name} | {message}");
    }

    public void Fail(string name, string message)
    {
        errors++;
        output.WriteLine($"FAIL {name} | {message}");
    }

    public int Complete()
    {
        string outcome = errors == 0 ? "PASS" : "FAIL";
        output.WriteLine($"INSPECTION {outcome} | warnings {warnings} | errors {errors}");
        if (errors > 0)
        {
            output.WriteLine("ACTION | Correct the reported scene, prefab or asset errors, then inspect again.");
        }
        return errors == 0 ? CliExitCodes.Success : CliExitCodes.CommandFailed;
    }
}
