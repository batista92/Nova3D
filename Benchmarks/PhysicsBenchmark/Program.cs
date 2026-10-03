using System.Diagnostics;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Nova3D.Physics.Bepu;
using Nova3D.Production.Scenes;
using Nova3D.World.Terrain;
using Nova3D.World.Streaming;

ValidateTerrainPhysics();
ValidateKinematicsAndTriggers();
ValidateShapeCasts();
ValidateConstraints();
ValidateCharacterController();
ValidateSceneFlowIntegration();
PhysicsGateResult gate = PhysicsGateValidation.Run();

using var physics = new BepuPhysicsWorld(new PhysicsWorldOptions
{
    FixedTimeStep = 1f / 60f,
    MaximumStepsPerUpdate = 4
});

physics.CreateStaticBox(
    new Vector3(0f, -0.5f, 0f),
    new Vector3(200f, 1f, 200f),
    collisionFilter: CollisionFilter.Layer(0, 1u << 0));
var probe = physics.CreateDynamicBox(new Vector3(0f, 10f, 0f), Vector3.One);
var sphere = physics.CreateDynamicSphere(new Vector3(-2f, 8f, 0f), 0.5f);
var capsule = physics.CreateDynamicCapsule(new Vector3(2f, 8f, 0f), 0.5f, 2f);
var removableStatic = physics.CreateStaticBox(new Vector3(500f, 0f, 0f), Vector3.One);
physics.Remove(removableStatic);

var testMaterial = new PhysicsMaterial(0.25f, 1.5f, 20f, 0.8f);
sphere.Material = testMaterial;
if (sphere.Material != testMaterial)
    throw new InvalidOperationException("Runtime material update was not preserved.");
var sphereFilter = CollisionFilter.Layer(0, 1u << 0);
sphere.CollisionFilter = sphereFilter;
if (sphere.CollisionFilter != sphereFilter)
    throw new InvalidOperationException("Runtime collision-filter update was not preserved.");

if (physics.Update(physics.FixedTimeStep * 0.5f) != 0 ||
    physics.Update(physics.FixedTimeStep * 0.5f) != 1)
    throw new InvalidOperationException("Fixed timestep accumulation is incorrect.");

for (int i = 0; i < 240; i++)
    physics.Update(1f / 60f);

if (probe.Position.Y is < 0.45f or > 0.65f)
    throw new InvalidOperationException($"Box did not settle on the ground: y={probe.Position.Y:F3}");
if (sphere.Position.Y is < 0.45f or > 0.65f)
    throw new InvalidOperationException($"Sphere did not settle on the ground: y={sphere.Position.Y:F3}");
if (capsule.Position.Y is < 1.4f or > 1.6f)
    throw new InvalidOperationException($"Capsule did not settle on the ground: y={capsule.Position.Y:F3}");

if (!physics.Raycast(new Vector3(0f, 5f, 0f), Vector3.Down, 10f, out PhysicsRayHit hit))
    throw new InvalidOperationException("Expected the downward ray to hit the scene.");

var ghost = physics.CreateDynamicBox(
    new Vector3(5f, 4f, 0f),
    Vector3.One,
    collisionFilter: CollisionFilter.Layer(1, 1u << 1));
for (int i = 0; i < 120; i++)
    physics.Update(1f / 60f);
if (ghost.Position.Y > -2f)
    throw new InvalidOperationException("Collision filtering did not reject the ground contact.");
physics.Remove(ghost);

physics.Remove(probe);
physics.Remove(sphere);
physics.Remove(capsule);
if (probe.Exists)
    throw new InvalidOperationException("Removed body still exists.");

const int bodyCount = 1000;
for (int i = 0; i < bodyCount; i++)
{
    int x = i % 40;
    int z = (i / 40) % 25;
    physics.CreateDynamicBox(
        new Vector3(x * 1.5f - 30f, 2f + (i % 5) * 1.2f, z * 1.5f - 18f),
        Vector3.One);
}

var stopwatch = Stopwatch.StartNew();
const int measuredSteps = 120;
for (int i = 0; i < measuredSteps; i++)
    physics.Update(1f / 60f);
stopwatch.Stop();

double millisecondsPerStep = stopwatch.Elapsed.TotalMilliseconds / measuredSteps;
Console.WriteLine(
    $"Physics P5 automated PASS | bodies {bodyCount} | {millisecondsPerStep:F3} ms/step | " +
    $"alloc {gate.AllocatedBytesPerStep} B/step | deterministic {gate.DeterminismHash:X16} | " +
    $"ray {hit.Distance:F3} m");

static void ValidateSceneFlowIntegration()
{
    using var physics = new BepuPhysicsWorld(new PhysicsWorldOptions { WorkerCount = 1 });
    using var scenes = new SceneService(new SceneInstantiator());
    var flow = new SceneFlowController(scenes);
    var adapter = new BepuSceneFlowAdapter(flow, physics);
    var observed = new List<SceneFlowState>();
    flow.StateChanged += (_, current) => observed.Add(current); // A UI only observes.

    using var properties = JsonDocument.Parse("{}");
    var registry = new SceneComponentRegistry();
    registry.Register(new TestSceneBodyDescriptor(physics));
    SceneLoadPlan MakePlan(string id) => SceneLoader.Prepare(
        new SceneDocument(SceneDocument.FormatName, SceneDocument.CurrentVersion, id,
        [new SceneNodeDocument(id, id, null,
            new SceneTransformDocument(new Vector3(0f, 10f, 0f), Vector3.Zero, Vector3.One),
            [new SceneComponentDocument("body", "test.body", properties.RootElement)])]),
        registry);
    var first = MakePlan("phase-a");
    var second = MakePlan("phase-b");

    flow.ReturnToMenu();
    if (adapter.Update(1f / 30f) != 0 || physics.BodyCount != 0)
        throw new InvalidOperationException("Menu must not step or retain scene bodies.");
    flow.Start(first);
    if (physics.BodyCount != 1 || !adapter.CanStep || adapter.Update(1f / 30f) == 0)
        throw new InvalidOperationException("Playing must step its scene-owned physics body.");
    var body = flow.ActiveScene!.GetComponents<BepuBodyBox>().Single().Body;
    var failingRegistry = new SceneComponentRegistry();
    failingRegistry.Register(new TestSceneBodyDescriptor(physics));
    failingRegistry.Register(new TestSceneFailureDescriptor());
    var failingPlan = SceneLoader.Prepare(
        new SceneDocument(SceneDocument.FormatName, SceneDocument.CurrentVersion, "failing",
        [new SceneNodeDocument("failing", "Failing", null,
            new SceneTransformDocument(new Vector3(0f, 10f, 0f), Vector3.Zero, Vector3.One),
            [new SceneComponentDocument("body", "test.body", properties.RootElement),
             new SceneComponentDocument("failure", "test.failure", properties.RootElement)])]),
        failingRegistry);
    try
    {
        flow.Start(failingPlan);
        throw new InvalidOperationException("Expected scene-body rollback test to fail.");
    }
    catch (SceneInstantiationException) { }
    if (flow.State != SceneFlowState.Playing || physics.BodyCount != 1 || !body.Exists)
        throw new InvalidOperationException("Failed phase replacement leaked a body or lost the old phase.");
    flow.Pause();
    var pausedPosition = body.Position;
    for (var i = 0; i < 4; i++)
    {
        if (adapter.Update(1f) != 0 || body.Position != pausedPosition)
            throw new InvalidOperationException("Paused physics advanced or accumulated elapsed time.");
    }
    flow.Resume();
    if (adapter.Update(1f / 30f) == 0 || body.Position == pausedPosition)
        throw new InvalidOperationException("Physics did not resume after pause.");
    flow.ShowResult();
    if (adapter.Update(1f / 30f) != 0)
        throw new InvalidOperationException("Result must not step physics.");
    flow.Restart();
    if (physics.BodyCount != 1 || body.Exists)
        throw new InvalidOperationException("Restart retained an old physics body.");

    for (var cycle = 0; cycle < 8; cycle++)
    {
        flow.ReturnToMenu();
        if (physics.BodyCount != 0 || physics.StaticCount != 0 || adapter.CanStep)
            throw new InvalidOperationException("Menu retained physics resources.");
        flow.Start(cycle % 2 == 0 ? second : first);
        if (physics.BodyCount != 1 || adapter.Update(1f / 30f) == 0)
            throw new InvalidOperationException("Phase switch did not initialize physics cleanly.");
    }
    flow.ReturnToMenu();
    if (physics.BodyCount != 0 || physics.StaticCount != 0 ||
        !observed.Contains(SceneFlowState.Loading) || !observed.Contains(SceneFlowState.Paused) ||
        observed[^1] != SceneFlowState.Menu)
        throw new InvalidOperationException("Flow/UI observation or physics cleanup regressed.");
}

static void ValidateTerrainPhysics()
{
    using var terrainWorld = new BepuPhysicsWorld(new PhysicsWorldOptions { WorkerCount = 1 });
    var heights = new DeformableHeightProvider(new DelegateHeightProvider((_, _) => 0f));
    using var terrain = new TerrainPhysics(terrainWorld, heights, new TerrainPhysicsSettings
    {
        WorldSize = 32f,
        ChunksPerAxis = 4,
        SegmentsPerChunk = 8
    });

    if (terrain.ChunkCount != 16 || terrainWorld.StaticCount != 16)
        throw new InvalidOperationException("Terrain chunk ownership is incorrect.");
    if (!terrainWorld.Raycast(new Vector3(0f, 10f, 0f), Vector3.Down, 20f, out PhysicsRayHit before) ||
        MathF.Abs(before.Distance - 10f) > 0.01f)
        throw new InvalidOperationException($"Initial terrain raycast is misaligned: {before.Distance:F3}");

    TerrainRegion changed = heights.ApplyRadialDelta(0f, 0f, 3f, 3f);
    int rebuilt = terrain.RebuildRegion(changed);
    if (rebuilt is < 1 or > 4)
        throw new InvalidOperationException($"Unexpected terrain rebuild count: {rebuilt}");
    if (!terrainWorld.Raycast(new Vector3(0f, 10f, 0f), Vector3.Down, 20f, out PhysicsRayHit after) ||
        MathF.Abs(after.Distance - 7f) > 0.01f)
        throw new InvalidOperationException($"Deformed terrain raycast is misaligned: {after.Distance:F3}");

    terrain.Dispose();
    if (terrainWorld.StaticCount != 0)
        throw new InvalidOperationException("Resident terrain did not release its colliders.");

    using var streamed = new TerrainPhysics(terrainWorld, heights, new TerrainPhysicsSettings
    {
        WorldSize = 32f,
        ChunksPerAxis = 4,
        SegmentsPerChunk = 8,
        Streaming = new WorldStreamingSettings
        {
            CellSize = 8f,
            Origin = new Vector2(-16f, -16f),
            LoadRadius = 1f,
            RetainRadius = 2f,
            MaxLoadsPerUpdate = 1,
            MaxUnloadsPerUpdate = 1
        }
    });
    streamed.Update(new Vector3(-12f, 0f, -12f));
    if (streamed.ChunkCount != 1 || streamed.ChunkLoadsLastUpdate != 1)
        throw new InvalidOperationException("Physics terrain streaming did not load the focused chunk.");
    streamed.Update(new Vector3(12f, 0f, 12f));
    if (streamed.ChunkCount != 1 || streamed.ChunkLoadsLastUpdate != 1 ||
        streamed.ChunkUnloadsLastUpdate != 1)
        throw new InvalidOperationException("Physics terrain streaming ownership is incorrect.");
}

static void ValidateKinematicsAndTriggers()
{
    using var triggerWorld = new BepuPhysicsWorld(new PhysicsWorldOptions
    {
        Gravity = Vector3.Zero,
        WorkerCount = 1
    });
    BepuBody trigger = triggerWorld.CreateKinematicBox(
        Vector3.Zero, new Vector3(4f), isTrigger: true);
    BepuBody visitor = triggerWorld.CreateDynamicSphere(Vector3.Zero, 0.5f);
    if (!trigger.IsKinematic || !trigger.IsTrigger)
        throw new InvalidOperationException("Kinematic trigger flags are incorrect.");

    triggerWorld.Update(triggerWorld.FixedTimeStep);
    if (!triggerWorld.TriggerEvents.Any(e => e.Type == TriggerEventType.Enter))
        throw new InvalidOperationException("Trigger enter event was not reported.");
    triggerWorld.Update(triggerWorld.FixedTimeStep);
    if (!triggerWorld.TriggerEvents.Any(e => e.Type == TriggerEventType.Stay))
        throw new InvalidOperationException("Trigger stay event was not reported.");

    visitor.SetPose(new Vector3(10f, 0f, 0f), Quaternion.Identity);
    triggerWorld.Update(triggerWorld.FixedTimeStep);
    if (!triggerWorld.TriggerEvents.Any(e => e.Type == TriggerEventType.Exit))
        throw new InvalidOperationException("Trigger exit event was not reported.");
}

static void ValidateShapeCasts()
{
    using var castWorld = new BepuPhysicsWorld(new PhysicsWorldOptions
    {
        Gravity = Vector3.Zero,
        WorkerCount = 1
    });
    castWorld.CreateStaticBox(new Vector3(5f, 0f, 0f), new Vector3(1f, 4f, 4f));
    castWorld.CreateKinematicBox(new Vector3(2f, 0f, 0f), new Vector3(1f), isTrigger: true);

    if (!castWorld.SphereCast(Vector3.Zero, 0.5f, Vector3.Right, 10f, out ShapeCastHit sphereHit) ||
        MathF.Abs(sphereHit.Distance - 4f) > 0.05f)
        throw new InvalidOperationException($"Sphere cast distance is incorrect: {sphereHit.Distance:F3}");
    if (!castWorld.SphereCast(Vector3.Zero, 0.5f, Vector3.Right, 10f,
            out ShapeCastHit triggerHit, includeTriggers: true) || triggerHit.Distance >= sphereHit.Distance)
        throw new InvalidOperationException("Shape cast did not include the trigger when requested.");
    if (!castWorld.CapsuleCast(Vector3.Zero, 0.5f, 1f, Quaternion.Identity,
            Vector3.Right, 10f, out ShapeCastHit capsuleHit) ||
        MathF.Abs(capsuleHit.Distance - 4f) > 0.05f)
        throw new InvalidOperationException($"Capsule cast distance is incorrect: {capsuleHit.Distance:F3}");
}

static void ValidateConstraints()
{
    using var constraintWorld = new BepuPhysicsWorld(new PhysicsWorldOptions
    {
        Gravity = Vector3.Zero,
        WorkerCount = 1
    });
    BepuBody a = constraintWorld.CreateDynamicSphere(Vector3.Zero, 0.25f);
    BepuBody b = constraintWorld.CreateDynamicSphere(new Vector3(5f, 0f, 0f), 0.25f);
    var constraint = constraintWorld.CreateDistanceConstraint(a, b, 2f);
    for (int i = 0; i < 120; i++) constraintWorld.Update(1f / 60f);
    float distance = Vector3.Distance(a.Position, b.Position);
    if (MathF.Abs(distance - 2f) > 0.05f)
        throw new InvalidOperationException($"Distance constraint did not converge: {distance:F3}");
    constraintWorld.Remove(constraint);
}

static void ValidateCharacterController()
{
    using var characterWorld = new BepuPhysicsWorld(new PhysicsWorldOptions
    {
        Gravity = Vector3.Zero,
        WorkerCount = 1
    });
    characterWorld.CreateStaticBox(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
    characterWorld.CreateStaticBox(new Vector3(3f, 2f, 0f), new Vector3(1f, 4f, 10f));

    var controller = new KinematicCharacterController(characterWorld, new Vector3(0f, 1.05f, 0f),
        new KinematicCharacterSettings
        {
            Radius = 0.5f,
            CapsuleLength = 1f,
            SkinWidth = 0.02f,
            GroundProbeDistance = 0.1f
        });
    controller.RefreshGrounded();
    if (!controller.IsGrounded || Vector3.Dot(controller.GroundNormal, Vector3.Up) < 0.99f)
        throw new InvalidOperationException("Character ground probing is incorrect.");

    controller.Teleport(new Vector3(0f, 2f, 0f));
    Vector3 blocked = controller.Move(new Vector3(10f, 0f, 0f));
    if (blocked.X is < 1.9f or > 2.05f)
        throw new InvalidOperationException($"Character crossed the wall: x={blocked.X:F3}");

    Vector3 slid = controller.Move(new Vector3(1f, 0f, 2f));
    if (slid.X > 2.05f || slid.Z < 1.9f)
        throw new InvalidOperationException($"Character sweep-and-slide is incorrect: {slid}");
}

sealed class BepuBodyBox(BepuBody body)
{
    public BepuBody Body { get; } = body;
}

sealed class TestSceneBodyDescriptor(BepuPhysicsWorld physics) : ISceneRuntimeComponentDescriptor
{
    public string Type => "test.body";
    public void Validate(SceneComponentValidationContext context) { }

    public object Create(SceneComponentInstantiationContext context) =>
        new BepuBodyBox(physics.CreateDynamicBox(context.WorldTransform.Translation, Vector3.One));

    public void Destroy(object instance) => physics.Remove(((BepuBodyBox)instance).Body);
}

sealed class TestSceneFailureDescriptor : ISceneRuntimeComponentDescriptor
{
    public string Type => "test.failure";
    public void Validate(SceneComponentValidationContext context) { }
    public object Create(SceneComponentInstantiationContext context) =>
        throw new InvalidOperationException("Intentional scene factory failure.");
    public void Destroy(object instance) { }
}
