# Physics

Physics is an optional Nova3D module backed by BEPUphysics 2. The rendering and
world toolkit does not require it.

## Installation

After installing the local Nova3D feed:

```powershell
dotnet add package Nova3D.Physics.Bepu --version 0.1.0
```

## Creating a world

```csharp
using Microsoft.Xna.Framework;
using Nova3D.Physics.Bepu;

using var physics = new BepuPhysicsWorld(new PhysicsWorldOptions
{
    Gravity = new Vector3(0f, -9.81f, 0f),
    FixedTimeStep = 1f / 60f
});

physics.CreateStaticBox(
    new Vector3(0f, -0.5f, 0f),
    new Vector3(100f, 1f, 100f));

BepuBody body = physics.CreateDynamicBox(
    new Vector3(0f, 10f, 0f),
    Vector3.One);
```

Call `Update` from the game update loop. The module accumulates elapsed time and
advances BEPU using the configured fixed step:

```csharp
physics.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
```

Use `body.WorldMatrix` to render a mesh at the simulated pose. Rendering remains
separate from simulation.

## Collision layers

```csharp
CollisionFilter terrain = CollisionFilter.Layer(0, 1u << 1);
CollisionFilter vehicles = CollisionFilter.Layer(1, (1u << 0) | (1u << 1));
```

Both membership masks must accept one another for contact generation. Up to 32
layers are available.

## Physical materials

```csharp
var ice = new PhysicsMaterial(
    Friction: 0.05f,
    MaximumRecoveryVelocity: 2f,
    SpringFrequency: 30f,
    SpringDampingRatio: 1f);

BepuBody body = physics.CreateDynamicBox(
    position,
    size,
    material: ice);

body.Material = PhysicsMaterial.Default;
```

Contact friction uses the geometric mean of both materials. Recovery velocity
uses the lower value, spring frequency uses the lower value, and damping uses
the higher value. Materials and collision filters can be changed at runtime.

## Debugging and statistics

```csharp
physics.DebugDraw(debugRenderer, Color.Lime);

int bodies = physics.BodyCount;
int statics = physics.StaticCount;
int steps = physics.LastStepCount;
double milliseconds = physics.LastStepMilliseconds;
```

`DebugDraw` supports the primitive colliders created through this module. Shapes
created directly through `Simulation` remain the caller's responsibility.

## Queries

```csharp
if (physics.Raycast(origin, direction, 100f, out PhysicsRayHit hit))
{
    Vector3 point = hit.Position;
    Vector3 normal = hit.Normal;
}
```

Sphere and capsule sweeps use the same collision filters as contacts. Triggers
are ignored by default, and a body can be excluded from the query:

```csharp
physics.CapsuleCast(origin, radius, length, rotation, direction, distance,
    out ShapeCastHit hit, playerFilter, includeTriggers: false,
    ignoredBody: playerBody);
```

## Kinematic character

`KinematicCharacterController` is a deliberately small capsule controller. It
provides filtered sweep-and-slide movement and a slope-aware ground probe while
leaving input, gravity, jumping and gameplay state in the game:

```csharp
var player = new KinematicCharacterController(physics, spawn,
    new KinematicCharacterSettings
    {
        Radius = 0.45f,
        CapsuleLength = 0.9f,
        MaximumSlopeDegrees = 50f,
        CollisionFilter = playerFilter
    });

player.Move(desiredDisplacement);
if (player.IsGrounded)
    Vector3 normal = player.GroundNormal;
```

Use `Teleport` for discontinuous movement. `Move` expects a displacement, not a
velocity; multiply gameplay velocity by elapsed time before calling it.

## Backend access

`BepuPhysicsWorld.Simulation` is public for constraints, custom shapes and other
advanced BEPU functionality. Nova3D intentionally does not wrap the entire BEPU
API.

The common distance servo has a convenience method:

```csharp
ConstraintHandle constraint = physics.CreateDistanceConstraint(a, b, 2f);
physics.Remove(constraint);
```

## Current scope

- fixed-timestep simulation;
- multithreaded stepping;
- dynamic box, sphere and capsule bodies;
- static boxes;
- closest-hit raycasts;
- 32 collision layers with membership/mask filtering;
- configurable physical materials;
- primitive collider debug draw;
- body/static counts and last-update timing;
- kinematic boxes and trigger enter/stay/exit events;
- sphere and capsule casts;
- distance constraints;
- capsule-based kinematic character movement and ground probing;
- explicit body and static removal;
- MonoGame/`System.Numerics` conversions.

## Terrain physics

`TerrainPhysics` generates one BEPU triangle mesh per terrain chunk from the
same `IHeightProvider` used by `ChunkedTerrain`:

```csharp
using var terrainPhysics = new TerrainPhysics(physics, heights,
    new TerrainPhysicsSettings
    {
        WorldSize = 2048f,
        ChunksPerAxis = 32,
        SegmentsPerChunk = 16
    });
```

For a runtime edit, mutate the shared height provider once and pass its region
to both owners:

```csharp
TerrainRegion changed = heights.ApplyRadialDelta(x, z, radius, delta);
visualTerrain.RebuildRegion(changed);
terrainPhysics.RebuildRegion(changed);
```

Set `TerrainPhysicsSettings.Streaming` to matching `WorldStreamingSettings` and
call `terrainPhysics.Update(focus)` alongside the visual terrain update. Loading,
unloading and regional rebuilds then create and release colliders per chunk.

Debug wire rendering for arbitrary triangle meshes is not included yet; use
raycasts and chunk statistics to diagnose physical terrain alignment.

## Physics Gate scene

The repository's CityBenchmark hosts the interactive validation without making
the core `Nova3D` project depend on BEPU:

- `F5` toggles cyan debug rendering for 100 dynamic boxes;
- `F6` recreates the dynamic stack near the initial camera position;
- `F2` deforms the terrain and rebuilds the same affected visual and physical
  chunks from their shared height provider.

The window title reports physics step time, dynamic body count and resident
physics chunks while the overlay is enabled. The headless `PhysicsBenchmark`
separately checks steady-state allocations and local single-thread determinism.
Multithread simulation is treated as a performance mode, not as a cross-run or
cross-platform determinism guarantee.
