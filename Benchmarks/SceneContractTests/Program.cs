using System.Text.Json;
using Microsoft.Xna.Framework;
using Nova3D.Production.Scenes;
using Nova3D.Production.Scenes.Assets;
using Nova3D.Production.Scenes.BuiltIns;
using Nova3D.Production.Scenes.Prefabs;

var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.scene.json");
var schemaFixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "nova3d.scene.1.schema.json");
var document = SceneDocumentSerializer.Load(fixture);

using (var schema = JsonDocument.Parse(File.ReadAllBytes(schemaFixture)))
{
    Require(schema.RootElement.GetProperty("$schema").GetString() ==
            "https://json-schema.org/draft/2020-12/schema", "schema dialect");
    Require(schema.RootElement.GetProperty("$id").GetString() ==
            "urn:nova3d:schema:scene:1", "schema ID");
    Require(schema.RootElement.GetProperty("properties").GetProperty("format")
            .GetProperty("const").GetString() == SceneDocument.FormatName, "schema format");
    Require(schema.RootElement.GetProperty("properties").GetProperty("version")
            .GetProperty("const").GetInt32() == SceneDocument.CurrentVersion, "schema version");
}

Require(document.Format == SceneDocument.FormatName, "format");
Require(document.Version == SceneDocument.CurrentVersion, "version");
Require(document.Name == "Minimal", "name");
Require(document.Nodes.Count == 1 && document.Nodes[0].Id == "origin", "minimal node");
Require(SceneDocumentValidator.Validate(document).IsValid, "minimal semantic validation");

var serialized = SceneDocumentSerializer.Serialize(document);
var roundTrip = SceneDocumentSerializer.Parse(serialized, "roundtrip.scene.json");
Require(roundTrip.Nodes.Count == 1, "round-trip node count");
Require(roundTrip.Nodes[0].Transform.Scale == Vector3.One, "round-trip transform");

using (var properties = JsonDocument.Parse("""{"asset":"Models/crate.glb"}"""))
{
    var authored = new SceneDocument(
        SceneDocument.FormatName,
        SceneDocument.CurrentVersion,
        "Authored",
        [
            new SceneNodeDocument(
                "crate",
                "Crate",
                null,
                new SceneTransformDocument(Vector3.Zero, new Vector3(360f, -181f, 540f), Vector3.One),
                [new SceneComponentDocument("visual", "nova3d.model", properties.RootElement)])
        ]);

    var authoredRoundTrip = SceneDocumentSerializer.Parse(SceneDocumentSerializer.Serialize(authored));
    Require(authoredRoundTrip.Nodes[0].Transform.RotationDegrees == new Vector3(0f, 179f, -180f),
        "canonical rotation");
    Require(authoredRoundTrip.Nodes[0].Components[0].Properties.GetProperty("asset").GetString() ==
            "Models/crate.glb", "detached component properties");
}

ExpectParseFailure(
    """{"format":"nova3d.scene","format":"nova3d.scene","version":1,"name":"Duplicate","nodes":[]}""",
    "$.format");
ExpectParseFailure(
    """{"format":"nova3d.scene","version":1,"name":"Unknown","nodes":[],"extra":true}""",
    "$.extra");
ExpectParseFailure(
    """
    {
      "format":"nova3d.scene","version":1,"name":"Nested Duplicate",
      "nodes":[{
        "id":"node","name":"Node",
        "transform":{"position":[0,0,0],"rotationDegrees":[0,0,0],"scale":[1,1,1]},
        "components":[{"id":"data","type":"game.data","properties":{"value":1,"value":2}}]
      }]
    }
    """,
    "$.nodes[0].components[0].properties.value");
ExpectParseFailure(
    """{"format":"nova3d.scene","version":1,"name":"Comment",/* no */"nodes":[]}""",
    "$");
ExpectParseFailure(
    """{"format":"nova3d.scene","version":1,"name":"Trailing","nodes":[],}""",
    "$");
ExpectParseFailure(
    """{"format":"nova3d.scene","version":1,"name":"Missing"}""",
    "$");
ExpectParseFailure(
    """
    {
      "format":"nova3d.scene",
      "version":1,
      "name":"Vector",
      "nodes":[{
        "id":"node","name":"Node",
        "transform":{"position":[0,0],"rotationDegrees":[0,0,0],"scale":[1,1,1]},
        "components":[]
      }]
    }
    """,
    "$.nodes[0].transform.position");

using (var emptyProperties = JsonDocument.Parse("{}"))
{
    var registry = new SceneComponentRegistry();
    registry.Register(new DelegateSceneComponentDescriptor("game.spawn"));
    Require(registry.Count == 1 && registry.GetRequired("game.spawn").Type == "game.spawn",
        "explicit component registry");
    ExpectException<InvalidOperationException>(
        () => registry.Register(new DelegateSceneComponentDescriptor("game.spawn")),
        "duplicate component registration");
    ExpectException<ArgumentException>(
        () => registry.Register(new DelegateSceneComponentDescriptor("InvalidType")),
        "invalid registered component type");

    var knownComponentDocument = new SceneDocument(
        SceneDocument.FormatName,
        SceneDocument.CurrentVersion,
        "Known component",
        [
            new SceneNodeDocument(
                "spawn",
                "Spawn",
                null,
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One),
                [new SceneComponentDocument("spawn", "game.spawn", emptyProperties.RootElement)])
        ]);
    Require(SceneDocumentValidator.Validate(knownComponentDocument, registry).IsValid,
        "registered component validation");

    var validatingRegistry = new SceneComponentRegistry();
    validatingRegistry.Register(new DelegateSceneComponentDescriptor("game.mover", context =>
    {
        if (!context.Component.Properties.TryGetProperty("speed", out var speed) ||
            speed.ValueKind != JsonValueKind.Number || speed.GetSingle() <= 0f)
        {
            context.Report("TST001", "Speed must be a positive number.", "speed");
        }
    }));
    var descriptorDocument = new SceneDocument(
        SceneDocument.FormatName,
        SceneDocument.CurrentVersion,
        "Descriptor",
        [
            new SceneNodeDocument(
                "mover", "Mover", null,
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One),
                [new SceneComponentDocument("movement", "game.mover", emptyProperties.RootElement)])
        ]);
    var descriptorValidation = SceneDocumentValidator.Validate(descriptorDocument, validatingRegistry);
    RequireIssue(descriptorValidation, "TST001", "$.nodes[0].components[0].properties.speed");

    var invalidDocument = new SceneDocument(
        "wrong.scene",
        99,
        " ",
        [
            new SceneNodeDocument(
                "duplicate", "First", "missing",
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One),
                [
                    new SceneComponentDocument("same", "game.unknown", emptyProperties.RootElement),
                    new SceneComponentDocument("same", "InvalidType", emptyProperties.RootElement)
                ]),
            new SceneNodeDocument(
                "duplicate", " ", null,
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.Zero), []),
            new SceneNodeDocument(
                "1invalid", "Invalid ID", "?",
                new SceneTransformDocument(
                    new Vector3(float.NaN, 0f, 0f),
                    new Vector3(0f, float.PositiveInfinity, 0f),
                    new Vector3(1f, float.NaN, 1f)), []),
            new SceneNodeDocument(
                "self", "Self", "self",
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One), []),
            new SceneNodeDocument(
                "cycle-a", "Cycle A", "cycle-b",
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One), []),
            new SceneNodeDocument(
                "cycle-b", "Cycle B", "cycle-a",
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One), [])
        ]);

    var validation = SceneDocumentValidator.Validate(invalidDocument, registry);
    Require(!validation.IsValid && validation.Issues.Count >= 14, "aggregated semantic errors");
    RequireIssue(validation, SceneValidationCodes.InvalidFormat, "$.format");
    RequireIssue(validation, SceneValidationCodes.UnsupportedVersion, "$.version");
    RequireIssue(validation, SceneValidationCodes.InvalidSceneName, "$.name");
    RequireIssue(validation, SceneValidationCodes.DuplicateNodeId, "$.nodes[1].id");
    RequireIssue(validation, SceneValidationCodes.InvalidNodeId, "$.nodes[2].id");
    RequireIssue(validation, SceneValidationCodes.MissingParent, "$.nodes[0].parent");
    RequireIssue(validation, SceneValidationCodes.SelfParent, "$.nodes[3].parent");
    RequireIssue(validation, SceneValidationCodes.ParentCycle, "$.nodes[4].parent");
    RequireIssue(validation, SceneValidationCodes.NonPositiveScale, "$.nodes[1].transform.scale");
    RequireIssue(validation, SceneValidationCodes.NonFinitePosition, "$.nodes[2].transform.position");
    RequireIssue(validation, SceneValidationCodes.NonFiniteRotation, "$.nodes[2].transform.rotationDegrees");
    RequireIssue(validation, SceneValidationCodes.NonFiniteScale, "$.nodes[2].transform.scale");
    RequireIssue(validation, SceneValidationCodes.DuplicateComponentId, "$.nodes[0].components[1].id");
    RequireIssue(validation, SceneValidationCodes.UnknownComponentType, "$.nodes[0].components[0].type");
    RequireIssue(validation, SceneValidationCodes.InvalidComponentType, "$.nodes[0].components[1].type");

    var repeatedValidation = SceneDocumentValidator.Validate(invalidDocument, registry);
    Require(validation.Issues.Select(issue => issue.ToString()).SequenceEqual(
        repeatedValidation.Issues.Select(issue => issue.ToString())), "deterministic issue order");
}

RunRuntimeTests();
RunSceneServiceTests();
RunSceneFlowTests();
RunBuiltInTests();
RunAssetTests();
RunPrefabTests();
RunAuthoredSampleTests();

Console.WriteLine("Scene G3.2 PASS | prefab reuse | transactional transitions | game flow");
return;

static void ExpectParseFailure(string json, string expectedPath)
{
    try
    {
        SceneDocumentSerializer.Parse(json, "invalid.scene.json");
        throw new InvalidOperationException($"Expected parsing to fail at '{expectedPath}'.");
    }
    catch (SceneDocumentParseException exception)
    {
        Require(exception.DocumentPath == "invalid.scene.json", "parse error document path");
        Require(exception.JsonPath == expectedPath ||
                (expectedPath == "$" && exception.JsonPath.StartsWith('$')),
            $"parse error JSON path (expected '{expectedPath}', got '{exception.JsonPath}')");
    }
}

static void Require(bool condition, string evidence)
{
    if (!condition)
        throw new InvalidOperationException($"Scene contract regression failed: {evidence}.");
}

static void RequireIssue(SceneValidationResult result, string code, string jsonPath)
{
    Require(result.Issues.Any(issue => issue.Code == code && issue.JsonPath == jsonPath),
        $"validation issue {code} at {jsonPath}");
}

static void ExpectException<TException>(Action action, string evidence) where TException : Exception
{
    try
    {
        action();
        throw new InvalidOperationException($"Expected {typeof(TException).Name}: {evidence}.");
    }
    catch (TException)
    {
    }
}

static void RunRuntimeTests()
{
    using var properties = JsonDocument.Parse("{}");
    var events = new List<string>();
    var descriptor = new ProbeRuntimeDescriptor("test.runtime", events);
    var registry = new SceneComponentRegistry();
    registry.Register(descriptor);

    var document = new SceneDocument(
        SceneDocument.FormatName,
        SceneDocument.CurrentVersion,
        "Runtime",
        [
            new SceneNodeDocument(
                "child", "Child", "root",
                new SceneTransformDocument(new Vector3(2f, 0f, 0f), Vector3.Zero, Vector3.One),
                [new SceneComponentDocument("runtime", "test.runtime", properties.RootElement)]),
            new SceneNodeDocument(
                "root", "Root", null,
                new SceneTransformDocument(new Vector3(10f, 0f, 0f), Vector3.Zero, Vector3.One),
                [new SceneComponentDocument("runtime", "test.runtime", properties.RootElement)])
        ]);

    var plan = SceneLoader.Prepare(document, registry, "runtime.scene.json");
    Require(plan.Nodes.Select(node => node.Id).SequenceEqual(["root", "child"]),
        "parent-before-child plan order");
    Require(plan.Nodes[1].WorldTransform.Translation == new Vector3(12f, 0f, 0f),
        "child world transform");

    var instantiator = new SceneInstantiator();
    var instance = instantiator.Instantiate(plan);
    Require(events.SequenceEqual(["create:root", "create:child"]), "deterministic create order");
    Require(instance.GetRequiredNode("child").TryGetComponent<RuntimeProbe>("runtime", out var childProbe) &&
            childProbe.NodeId == "child", "typed runtime component lookup");

    ExpectException<InvalidOperationException>(
        () => Task.Run(instance.Dispose).GetAwaiter().GetResult(),
        "disposal on a non-owning thread");
    Require(!instance.IsDisposed, "wrong-thread disposal leaves instance alive");
    instance.Dispose();
    Require(events.SequenceEqual(["create:root", "create:child", "destroy:child", "destroy:root"]),
        "reverse destroy order");
    instance.Dispose();
    Require(events.Count == 4 && instance.IsDisposed, "idempotent scene disposal");

    ExpectException<InvalidOperationException>(
        () => Task.Run(() => instantiator.Instantiate(plan)).GetAwaiter().GetResult(),
        "instantiation on a non-owning thread");

    var invalidDocument = new SceneDocument(
        SceneDocument.FormatName,
        999,
        "Invalid",
        []);
    try
    {
        SceneLoader.Prepare(invalidDocument, registry, "invalid-runtime.scene.json");
        throw new InvalidOperationException("Expected invalid scene preparation to fail.");
    }
    catch (SceneDocumentValidationException exception)
    {
        Require(exception.DocumentPath == "invalid-runtime.scene.json", "validation exception document path");
        Require(exception.Issues.Any(issue => issue.Code == SceneValidationCodes.UnsupportedVersion),
            "validation exception issues");
    }

    events.Clear();
    var failingRegistry = new SceneComponentRegistry();
    failingRegistry.Register(new ProbeRuntimeDescriptor("test.runtime", events, failNodeId: "child"));
    var failingPlan = SceneLoader.Prepare(document, failingRegistry, "rollback.scene.json");
    try
    {
        new SceneInstantiator().Instantiate(failingPlan);
        throw new InvalidOperationException("Expected scene instantiation to fail.");
    }
    catch (SceneInstantiationException exception)
    {
        Require(exception.NodeId == "child" && exception.ComponentId == "runtime",
            "instantiation failure context");
    }
    Require(events.SequenceEqual(["create:root", "create:child", "destroy:root"]),
        "partial instantiation rollback");
}

static void RunSceneServiceTests()
{
    using var properties = JsonDocument.Parse("{}");
    var events = new List<string>();
    var registry = new SceneComponentRegistry();
    registry.Register(new ProbeRuntimeDescriptor("test.runtime", events));
    SceneLoadPlan Plan(string id, SceneComponentRegistry selected) => SceneLoader.Prepare(
        new SceneDocument(SceneDocument.FormatName, SceneDocument.CurrentVersion, id,
        [new SceneNodeDocument(id, id, null,
            new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One),
            [new SceneComponentDocument("runtime", "test.runtime", properties.RootElement)])]),
        selected);

    var firstPlan = Plan("first", registry);
    var secondPlan = Plan("second", registry);
    using var service = new SceneService(new SceneInstantiator());
    var stages = new List<SceneTransitionStage>();
    var progress = new TestSceneProgress(stage => stages.Add(stage));
    var first = service.Load(_ => firstPlan, progress);
    Require(ReferenceEquals(service.Active, first) && !first.IsDisposed, "first scene active");
    Require(stages.SequenceEqual([SceneTransitionStage.Preparing, SceneTransitionStage.Instantiating,
        SceneTransitionStage.Unloading, SceneTransitionStage.Complete]), "UI-neutral progress stages");

    ExpectException<InvalidOperationException>(
        () => service.Load(_ => { service.Unload(); return secondPlan; }), "nested transition rejected");
    Require(ReferenceEquals(service.Active, first), "nested transition preserves first scene");
    ExpectException<InvalidOperationException>(
        () => service.Load(_ => throw new InvalidOperationException("invalid scene")),
        "preparation failure");
    Require(ReferenceEquals(service.Active, first), "preparation failure preserves first scene");

    var failingRegistry = new SceneComponentRegistry();
    failingRegistry.Register(new ProbeRuntimeDescriptor("test.runtime", events, "bad"));
    ExpectException<SceneInstantiationException>(
        () => service.Activate(Plan("bad", failingRegistry)), "factory failure");
    Require(ReferenceEquals(service.Active, first), "factory failure preserves first scene");

    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    ExpectException<OperationCanceledException>(
        () => service.Activate(secondPlan, cancellationToken: cancelled.Token), "cancelled transition");
    Require(ReferenceEquals(service.Active, first), "cancellation preserves first scene");

    var second = service.Activate(secondPlan);
    Require(first.IsDisposed && ReferenceEquals(service.Active, second), "atomic scene replacement");
    var reloaded = service.Reload();
    Require(second.IsDisposed && ReferenceEquals(service.Active, reloaded), "reload replaces scene");
    service.Unload();
    Require(reloaded.IsDisposed && service.Active is null, "unload disposes active scene");
    Require(events.Count(item => item is "create:first" or "create:second") ==
            events.Count(item => item is "destroy:first" or "destroy:second"),
        "all created values destroyed");
    ExpectException<InvalidOperationException>(() => service.Reload(), "reload without active scene");
    ExpectException<InvalidOperationException>(
        () => Task.Run(() => service.Unload()).GetAwaiter().GetResult(), "service thread affinity");
}

static void RunSceneFlowTests()
{
    Require(!typeof(SceneFlowController).Assembly.GetReferencedAssemblies().Any(reference =>
            reference.Name is "Nova3D.Physics.Bepu" or "Nova3D.UI.Gum"),
        "core scene flow has no optional physics or UI dependency");
    using var properties = JsonDocument.Parse("{}");
    var events = new List<string>();
    var registry = new SceneComponentRegistry();
    registry.Register(new ProbeRuntimeDescriptor("test.runtime", events));
    SceneLoadPlan Plan(string id) => SceneLoader.Prepare(
        new SceneDocument(SceneDocument.FormatName, SceneDocument.CurrentVersion, id,
        [new SceneNodeDocument(id, id, null,
            new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One),
            [new SceneComponentDocument("runtime", "test.runtime", properties.RootElement)])]),
        registry);

    using var scenes = new SceneService(new SceneInstantiator());
    var flow = new SceneFlowController(scenes);
    var changes = new List<(SceneFlowState Previous, SceneFlowState Next)>();
    flow.StateChanged += (previous, next) => changes.Add((previous, next));
    Require(flow.State == SceneFlowState.Boot, "initial boot state");
    ExpectException<InvalidOperationException>(() => flow.Restart(), "restart without scene");
    ExpectException<InvalidOperationException>(() => flow.Pause(), "pause without playing");

    flow.ReturnToMenu();
    Require(flow.State == SceneFlowState.Menu && scenes.Active is null, "empty menu");
    var first = Plan("level-a");
    var second = Plan("level-b");
    flow.Start(_ => first);
    Require(flow.State == SceneFlowState.Playing && scenes.Active?.Plan == first,
        "menu to first level");
    Require(changes.Contains((SceneFlowState.Menu, SceneFlowState.Loading)) &&
            changes.Contains((SceneFlowState.Loading, SceneFlowState.Playing)),
        "loading and playing hooks");

    flow.Pause();
    Require(flow.State == SceneFlowState.Paused && scenes.Active is not null,
        "pause retains scene");
    flow.Resume();
    flow.ShowResult();
    Require(flow.State == SceneFlowState.Result, "result is game-selected phase");
    flow.Restart();
    Require(flow.State == SceneFlowState.Playing && scenes.Active?.Plan == first,
        "restart re-instantiates current level");

    var activeBeforeFailure = scenes.Active;
    ExpectException<InvalidOperationException>(
        () => flow.Start(_ => throw new InvalidOperationException("invalid level")),
        "failed level preparation");
    Require(flow.State == SceneFlowState.Playing && ReferenceEquals(scenes.Active, activeBeforeFailure),
        "failed level restores prior phase and scene");
    flow.StateChanged += (_, next) =>
    {
        if (next == SceneFlowState.Loading)
            ExpectException<InvalidOperationException>(() => flow.ReturnToMenu(), "nested flow transition");
    };

    flow.Start(second);
    Require(flow.State == SceneFlowState.Playing && scenes.Active?.Plan == second,
        "second level replaces first");
    for (var cycle = 0; cycle < 4; cycle++)
    {
        flow.ReturnToMenu();
        Require(flow.State == SceneFlowState.Menu && scenes.Active is null,
            "menu unloads previous level");
        flow.Start(cycle % 2 == 0 ? first : second);
    }
    flow.ReturnToMenu();
    Require(events.Count(item => item.StartsWith("create:", StringComparison.Ordinal)) ==
            events.Count(item => item.StartsWith("destroy:", StringComparison.Ordinal)),
        "repeated menu and level transitions leave no runtime values alive");
}

static void RunBuiltInTests()
{
    var registry = new SceneComponentRegistry();
    var modelDescriptor = new SceneModelComponentDescriptor(
        (Func<SceneComponentInstantiationContext, string, Nova3D.Production.Assets.Gltf.GltfModel>)
        ((_, _) => throw new InvalidOperationException("Model creation is not part of this CPU regression.")),
        _ => throw new InvalidOperationException("Renderer creation is not part of this CPU regression."));
    registry.RegisterNova3DBuiltIns(modelDescriptor);
    Require(registry.Count == 5, "complete built-in component registration");

    using var cameraProperties = JsonDocument.Parse(
        """{"primary":true,"fieldOfViewDegrees":60,"nearPlane":0.25,"farPlane":500}""");
    using var lightProperties = JsonDocument.Parse(
        """{"color":[1.5,0.75,0.25],"intensity":2.5,"castShadows":false}""");
    using var spawnProperties = JsonDocument.Parse("""{"kind":"player"}""");
    using var tagProperties = JsonDocument.Parse("""{"value":"start-area"}""");

    var transform = new SceneTransformDocument(
        new Vector3(3f, 4f, 5f), new Vector3(0f, 90f, 0f), Vector3.One);
    var document = new SceneDocument(
        SceneDocument.FormatName,
        SceneDocument.CurrentVersion,
        "Built-ins",
        [
            new SceneNodeDocument(
                "origin", "Origin", null, transform,
                [
                    new SceneComponentDocument("camera", SceneCameraComponentDescriptor.ComponentType,
                        cameraProperties.RootElement),
                    new SceneComponentDocument("sun", SceneDirectionalLightComponentDescriptor.ComponentType,
                        lightProperties.RootElement),
                    new SceneComponentDocument("spawn", SceneSpawnPointDescriptor.ComponentType,
                        spawnProperties.RootElement),
                    new SceneComponentDocument("tag", SceneTagDescriptor.ComponentType,
                        tagProperties.RootElement)
                ])
        ]);

    var plan = SceneLoader.Prepare(document, registry, "built-ins.scene.json");
    using var instance = new SceneInstantiator().Instantiate(plan);
    var node = instance.GetRequiredNode("origin");
    Require(node.TryGetComponent<SceneCameraComponent>("camera", out var camera),
        "camera component lookup");
    Require(instance.GetComponents<SceneCameraComponent>().Single() == camera,
        "scene-wide typed component lookup");
    Require(camera.IsPrimary && camera.Camera.Position == new Vector3(3f, 4f, 5f),
        "camera primary flag and position");
    Require(Math.Abs(MathHelper.ToDegrees(camera.Camera.FieldOfView) - 60f) < 0.001f &&
            camera.Camera.NearPlane == 0.25f && camera.Camera.FarPlane == 500f,
        "camera projection properties");

    Require(node.TryGetComponent<SceneDirectionalLightComponent>("sun", out var sun),
        "directional light component lookup");
    Require(sun.Light.Color == new Vector3(1.5f, 0.75f, 0.25f) &&
            sun.Light.Intensity == 2.5f && !sun.CastsShadows,
        "directional light properties");
    Require(Vector3.Distance(camera.Camera.Direction, sun.Light.Direction) < 0.0001f,
        "camera and light use node forward axis");

    Require(node.TryGetComponent<SceneSpawnPoint>("spawn", out var spawn) &&
            spawn.Kind == "player" && spawn.Position == new Vector3(3f, 4f, 5f),
        "spawn component data");
    Require(node.TryGetComponent<SceneTag>("tag", out var tag) && tag.Value == "start-area",
        "tag component data");

    using var modelProperties = JsonDocument.Parse(
        """{"asset":"Models/crate.glb","visible":true,"castShadows":true}""");
    var modelDocument = new SceneDocument(
        SceneDocument.FormatName,
        SceneDocument.CurrentVersion,
        "Model",
        [
            new SceneNodeDocument(
                "crate", "Crate", null,
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One),
                [new SceneComponentDocument("visual", SceneModelComponentDescriptor.ComponentType,
                    modelProperties.RootElement)])
        ]);
    Require(SceneDocumentValidator.Validate(modelDocument, registry).IsValid,
        "portable GLB model component");

    using var invalidModelProperties = JsonDocument.Parse(
        """{"asset":"../crate.obj","unknown":1}""");
    var invalidModelDocument = new SceneDocument(
        SceneDocument.FormatName,
        SceneDocument.CurrentVersion,
        "Invalid model",
        [
            new SceneNodeDocument(
                "crate", "Crate", null,
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One),
                [new SceneComponentDocument("visual", SceneModelComponentDescriptor.ComponentType,
                    invalidModelProperties.RootElement)])
        ]);
    var invalid = SceneDocumentValidator.Validate(invalidModelDocument, registry);
    RequireIssue(invalid, SceneBuiltInValidationCodes.InvalidPropertyValue,
        "$.nodes[0].components[0].properties.asset");
    RequireIssue(invalid, SceneBuiltInValidationCodes.UnknownProperty,
        "$.nodes[0].components[0].properties.unknown");

    var extensibleRegistry = new SceneComponentRegistry();
    extensibleRegistry.RegisterNova3DBuiltIns();
    extensibleRegistry.Register(new ProbeRuntimeDescriptor("physics.rigid-body", []));
    Require(extensibleRegistry.Count == 5 && extensibleRegistry.TryGet("physics.rigid-body", out _),
        "optional module extension through explicit registry");
}

static void RunAssetTests()
{
    using var emptyProperties = JsonDocument.Parse("{}");
    var registry = new SceneComponentRegistry();
    registry.Register(new DelegateSceneComponentDescriptor("test.marker"));
    var document = new SceneDocument(
        SceneDocument.FormatName,
        SceneDocument.CurrentVersion,
        "Assets",
        [
            new SceneNodeDocument(
                "root", "Root", null,
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One),
                [new SceneComponentDocument("marker", "test.marker", emptyProperties.RootElement)])
        ]);
    var plan = SceneLoader.Prepare(document, registry, "Assets/Scenes/assets.scene.json");

    var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    var resolver = new SceneAssetResolver(fixtureRoot);
    var resolved = resolver.ResolveExistingFile(plan, "minimal.scene.json");
    Require(File.Exists(resolved) && Path.GetFileName(resolved) == "minimal.scene.json",
        "asset root resolution");

    try
    {
        resolver.ResolveExistingFile(plan, "missing.glb");
        throw new InvalidOperationException("Expected missing scene asset diagnostic.");
    }
    catch (SceneAssetNotFoundException exception)
    {
        Require(exception.ScenePath == plan.DocumentPath &&
                exception.AssetReference == "missing.glb" &&
                Path.GetFileName(exception.ResolvedPath) == "missing.glb",
            "missing asset diagnostic context");
    }
    ExpectException<SceneAssetPathException>(
        () => resolver.ResolveExistingFile(plan, "../minimal.scene.json"),
        "asset root escape rejection");
    ExpectException<IOException>(
        () => resolver.ResolveExistingFile(plan, "MINIMAL.SCENE.JSON"),
        "portable exact asset casing");

    var loads = 0;
    var destroys = 0;
    var cache = new SceneAssetCache<DisposableAsset>(
        (path, token) =>
        {
            token.ThrowIfCancellationRequested();
            loads++;
            return new DisposableAsset(path);
        },
        asset =>
        {
            asset.Dispose();
            destroys++;
        });
    var first = cache.Acquire(resolved);
    var second = cache.Acquire(resolved);
    Require(ReferenceEquals(first.Value, second.Value) && loads == 1 && cache.Count == 1,
        "shared cached asset instance");
    ExpectException<InvalidOperationException>(
        () => Task.Run(first.Dispose).GetAwaiter().GetResult(),
        "asset lease disposal thread affinity");
    Require(!first.IsDisposed, "failed wrong-thread lease disposal remains retryable");
    first.Dispose();
    Require(destroys == 0, "retained cache does not destroy a referenced asset");
    cache.Dispose();
    Require(destroys == 1 && second.IsDisposed, "cache shutdown destroys retained asset once");
    ExpectException<ObjectDisposedException>(() => _ = second.Value,
        "lease cannot expose a destroyed cache value");
    second.Dispose();
    Require(destroys == 1, "late lease release does not double destroy");

    var eagerDestroys = 0;
    using (var eagerCache = new SceneAssetCache<DisposableAsset>(
               (path, _) => new DisposableAsset(path),
               asset => { asset.Dispose(); eagerDestroys++; },
               keepUnusedAssets: false))
    {
        using var lease = eagerCache.Acquire(resolved);
    }
    Require(eagerDestroys == 1, "non-retaining cache unloads after final lease");

    var canceledBeforeLoad = new CancellationToken(canceled: true);
    using var unusedCache = new SceneAssetCache<DisposableAsset>(
        (path, _) => throw new InvalidOperationException($"Unexpected load: {path}"),
        asset => asset.Dispose());
    ExpectException<OperationCanceledException>(
        () => unusedCache.Acquire(resolved, canceledBeforeLoad),
        "cancellation before asset allocation");

    using var duringLoadCancellation = new CancellationTokenSource();
    var canceledAssetDestroys = 0;
    using var cancelingCache = new SceneAssetCache<DisposableAsset>(
        (path, _) =>
        {
            duringLoadCancellation.Cancel();
            return new DisposableAsset(path);
        },
        asset => { asset.Dispose(); canceledAssetDestroys++; });
    ExpectException<OperationCanceledException>(
        () => cancelingCache.Acquire(resolved, duringLoadCancellation.Token),
        "cancellation after asset allocation");
    Require(cancelingCache.Count == 0 && canceledAssetDestroys == 1,
        "canceled asset acquisition cleans unpublished value");

    var cancellationEvents = new List<string>();
    using var cancellation = new CancellationTokenSource();
    var cancellationRegistry = new SceneComponentRegistry();
    cancellationRegistry.Register(new CancelingRuntimeDescriptor(cancellation, cancellationEvents));
    using var runtimeProperties = JsonDocument.Parse("{}");
    var cancellationDocument = new SceneDocument(
        SceneDocument.FormatName,
        SceneDocument.CurrentVersion,
        "Cancellation",
        [
            new SceneNodeDocument(
                "root", "Root", null,
                new SceneTransformDocument(Vector3.Zero, Vector3.Zero, Vector3.One),
                [new SceneComponentDocument("runtime", "test.cancel", runtimeProperties.RootElement)])
        ]);
    var cancellationPlan = SceneLoader.Prepare(
        cancellationDocument, cancellationRegistry, "cancel.scene.json");
    ExpectException<OperationCanceledException>(
        () => new SceneInstantiator().Instantiate(cancellationPlan, cancellation.Token),
        "cancellation after runtime allocation");
    Require(cancellationEvents.SequenceEqual(["create", "destroy"]),
        "cancellation rolls back created runtime values");
}

static void RunPrefabTests()
{
    var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    var scenePath = Path.Combine(fixtureRoot, "prefab-demo.scene.json");
    var registry = new SceneComponentRegistry();
    registry.RegisterNova3DBuiltIns();
    var resolver = new SceneAssetResolver(fixtureRoot);

    var plan = ScenePrefabLoader.Prepare(scenePath, registry, resolver);
    Require(plan.Nodes.Count == 8, "two houses each expand to body, nested anchor and roof");
    Require(plan.Nodes.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() == 8,
        "prefab instance IDs are isolated");
    var bodyA = plan.Nodes.Single(node => node.Id == "house-a.body");
    var bodyB = plan.Nodes.Single(node => node.Id == "house-b.body");
    Require(bodyA.ParentId == "house-a" && bodyB.ParentId == "house-b",
        "prefab roots attach to their own anchor");
    Require(bodyA.WorldTransform.Translation == new Vector3(10f, 1f, 0f) &&
            bodyB.WorldTransform.Translation == new Vector3(20f, 0f, 0f),
        "typed transform override applies to only one instance");
    Require(plan.Nodes.Any(node => node.Id == "house-a.roof-anchor.roof" &&
                                   node.ParentId == "house-a.roof-anchor"),
        "nested prefab hierarchy preserves namespaced IDs");
    Require(plan.Nodes.All(node => node.Document.Components.All(component =>
        component.Type != ScenePrefabComponentDescriptor.ComponentType)),
        "runtime plan contains only expanded concrete components");

    using (var instance = new SceneInstantiator().Instantiate(plan))
    {
        Require(instance.GetRequiredNode("house-a.body")
                    .TryGetComponent<SceneTag>("tag", out var tagA) &&
                instance.GetRequiredNode("house-b.body")
                    .TryGetComponent<SceneTag>("tag", out var tagB) &&
                tagA.Value == "red-house" && tagB.Value == "house",
            "component override does not mutate the shared prefab document");
    }

    var cyclePath = Path.Combine(fixtureRoot, "Prefabs", "cycle-a.scene.json");
    try
    {
        ScenePrefabLoader.Prepare(cyclePath, registry, resolver);
        throw new InvalidOperationException("Expected recursive prefab reference failure.");
    }
    catch (ScenePrefabException error)
    {
        Require(error.Message.Contains("Recursive prefab", StringComparison.Ordinal),
            "recursive prefab gives actionable diagnostic");
    }

    var invalidType = SceneDocumentSerializer.Parse("""
        {"format":"nova3d.scene","version":1,"name":"Invalid override","nodes":[
          {"id":"house","name":"House","transform":{"position":[0,0,0],"rotationDegrees":[0,0,0],"scale":[1,1,1]},
           "components":[{"id":"prefab","type":"nova3d.prefab","properties":{
             "asset":"Prefabs/house.scene.json",
             "overrides":[{"node":"body","component":"tag","property":"value","value":42}]}}]}
        ]}
        """);
    ExpectException<ScenePrefabException>(
        () => ScenePrefabLoader.Prepare(invalidType, scenePath, registry, resolver),
        "typed component override rejects a number for a string");

    var invalidNode = SceneDocumentSerializer.Parse("""
        {"format":"nova3d.scene","version":1,"name":"Invalid node","nodes":[
          {"id":"house","name":"House","transform":{"position":[0,0,0],"rotationDegrees":[0,0,0],"scale":[1,1,1]},
           "components":[{"id":"prefab","type":"nova3d.prefab","properties":{
             "asset":"Prefabs/house.scene.json",
             "overrides":[{"node":"absent","property":"position","value":[0,0,0]}]}}]}
        ]}
        """);
    ExpectException<ScenePrefabException>(
        () => ScenePrefabLoader.Prepare(invalidNode, scenePath, registry, resolver),
        "override target must exist");

    var semanticallyInvalid = SceneDocumentSerializer.Parse("""
        {"format":"nova3d.scene","version":1,"name":"Invalid component override","nodes":[
          {"id":"house","name":"House","transform":{"position":[0,0,0],"rotationDegrees":[0,0,0],"scale":[1,1,1]},
           "components":[{"id":"prefab","type":"nova3d.prefab","properties":{
             "asset":"Prefabs/house.scene.json",
             "overrides":[{"node":"body","component":"tag","property":"value","value":""}]}}]}
        ]}
        """);
    try
    {
        ScenePrefabLoader.Prepare(semanticallyInvalid, scenePath, registry, resolver);
        throw new InvalidOperationException("Expected overridden component validation to fail.");
    }
    catch (SceneDocumentValidationException error)
    {
        Require(error.Issues.Any(issue => issue.Code == SceneBuiltInValidationCodes.InvalidPropertyValue),
            "overridden component is validated before runtime allocation");
    }
}

static void RunAuthoredSampleTests()
{
    var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    var registry = new SceneComponentRegistry();
    registry.RegisterNova3DBuiltIns();

    var samplePlan = ScenePrefabLoader.Prepare(
        Path.Combine(fixtureRoot, "sample-level.scene.json"),
        registry,
        new SceneAssetResolver(fixtureRoot));
    using (var scene = new SceneInstantiator().Instantiate(samplePlan))
    {
        Require(scene.GetComponents<SceneCameraComponent>().Count(camera => camera.IsPrimary) == 1,
            "data-driven sample has one primary camera");
        Require(scene.GetComponents<SceneTag>().Count() == 4,
            "data-driven sample expands two nested prefab instances");
    }

    var templateAssets = Path.Combine(fixtureRoot, "TemplateScene", "Assets");
    var templatePlan = ScenePrefabLoader.Prepare(
        Path.Combine(templateAssets, "Scenes", "starter.scene.json"),
        registry,
        new SceneAssetResolver(templateAssets));
    using var templateScene = new SceneInstantiator().Instantiate(templatePlan);
    Require(templateScene.GetComponents<SceneCameraComponent>().Count(camera => camera.IsPrimary) == 1,
        "template scene has one primary camera");
    Require(templateScene.GetComponents<SceneTag>().Select(tag => tag.Value).OrderBy(value => value)
            .SequenceEqual(["left", "right"]),
        "template prefabs have independent typed overrides");
}

sealed class RuntimeProbe
{
    public RuntimeProbe(string nodeId) => NodeId = nodeId;
    public string NodeId { get; }
}

sealed class TestSceneProgress(Action<SceneTransitionStage> callback) : IProgress<SceneTransitionProgress>
{
    public void Report(SceneTransitionProgress value) => callback(value.Stage);
}

sealed class ProbeRuntimeDescriptor : ISceneRuntimeComponentDescriptor
{
    private readonly IList<string> _events;
    private readonly string? _failNodeId;

    public ProbeRuntimeDescriptor(string type, IList<string> events, string? failNodeId = null)
    {
        Type = type;
        _events = events;
        _failNodeId = failNodeId;
    }

    public string Type { get; }

    public void Validate(SceneComponentValidationContext context)
    {
    }

    public object Create(SceneComponentInstantiationContext context)
    {
        _events.Add($"create:{context.Node.Id}");
        if (string.Equals(context.Node.Id, _failNodeId, StringComparison.Ordinal))
            throw new InvalidOperationException("Intentional runtime factory failure.");
        return new RuntimeProbe(context.Node.Id);
    }

    public void Destroy(object instance)
    {
        var probe = (RuntimeProbe)instance;
        _events.Add($"destroy:{probe.NodeId}");
    }
}

sealed class DisposableAsset : IDisposable
{
    public DisposableAsset(string path) => Path = path;
    public string Path { get; }
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

sealed class CancelingRuntimeDescriptor : ISceneRuntimeComponentDescriptor
{
    private readonly CancellationTokenSource _cancellation;
    private readonly IList<string> _events;

    public CancelingRuntimeDescriptor(CancellationTokenSource cancellation, IList<string> events)
    {
        _cancellation = cancellation;
        _events = events;
    }

    public string Type => "test.cancel";
    public void Validate(SceneComponentValidationContext context) { }

    public object Create(SceneComponentInstantiationContext context)
    {
        if (context.CancellationToken != _cancellation.Token)
            throw new InvalidOperationException("Factory did not receive the instantiation cancellation token.");
        _events.Add("create");
        _cancellation.Cancel();
        return new object();
    }

    public void Destroy(object instance) => _events.Add("destroy");
}
