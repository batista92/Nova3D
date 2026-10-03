# Nova3D scene format

`nova3d.scene` is the versioned, data-only format used to describe scene
composition. It does not contain gameplay scripts, service references or live
MonoGame resources. Parsing and structural validation must work without a
`GraphicsDevice`.

The first contract is `nova3d.scene` version `1`, referred to as `scene/1` in
documentation. G1 defines the persisted contract; G2 adds runtime components,
asset management and prefab expansion.

## Minimal document

```json
{
  "format": "nova3d.scene",
  "version": 1,
  "name": "Minimal",
  "nodes": [
    {
      "id": "origin",
      "name": "Origin",
      "transform": {
        "position": [0.0, 0.0, 0.0],
        "rotationDegrees": [0.0, 0.0, 0.0],
        "scale": [1.0, 1.0, 1.0]
      },
      "components": []
    }
  ]
}
```

The same document is available as
[`Examples/Scenes/minimal.scene.json`](Examples/Scenes/minimal.scene.json).
The structural Draft 2020-12 schema is
[`Schemas/nova3d.scene.1.schema.json`](Schemas/nova3d.scene.1.schema.json).
It provides editor/tool feedback for local object shape; the C# validator is
still required for cross-node and registered-component rules.

Validate a scene, its built-in components, prefabs and referenced model paths
without creating runtime or GPU resources:

```powershell
nova3d inspect Assets\Scenes\level01.scene.json
```

The CLI uses the same parser, validator and prefab loader as the runtime. It
registers Nova3D built-ins only; game-owned component descriptors still require
validation inside the game because the CLI does not scan or load game code.

## CPU API

`SceneDocumentSerializer` reads strict UTF-8 JSON without a window or
`GraphicsDevice`:

```csharp
using Nova3D.Production.Scenes;

SceneDocument scene = SceneDocumentSerializer.Load("Assets/Scenes/level01.scene.json");

foreach (SceneNodeDocument node in scene.Nodes)
    Console.WriteLine($"{node.Id}: {node.Transform.Position}");

SceneDocumentSerializer.Save("Assets/Scenes/level01.copy.scene.json", scene);
```

Use `Parse` for in-memory text or UTF-8 bytes and `Serialize`/`SerializeToUtf8`
when the caller owns output storage. The serializer preserves node/component
order, writes properties in canonical scene order and normalizes rotations.

Malformed JSON or a structural mismatch throws `SceneDocumentParseException`.
Its `DocumentPath` and `JsonPath` identify the source and failing property;
syntax failures also expose line and byte position when System.Text.Json can
provide them.

The G1.2 parser validates JSON shape, required properties and representable
numbers only. Semantic validation—format/version support, ID grammar,
duplicates, hierarchy, positive scale and registered component types—belongs
to `SceneDocumentValidator`. A successfully parsed document is not yet proof
that it can be instantiated.

```csharp
using System.Text.Json;

var components = new SceneComponentRegistry();
components.Register(new DelegateSceneComponentDescriptor("mygame.spawn", context =>
{
    if (!context.Component.Properties.TryGetProperty("kind", out var kind) ||
        kind.ValueKind != JsonValueKind.String)
    {
        context.Report("GAME001", "Spawn kind must be a string.", "kind");
    }
}));

SceneValidationResult validation = SceneDocumentValidator.Validate(scene, components);

foreach (SceneValidationIssue issue in validation.Issues)
    Console.WriteLine(issue); // SCN204 $.nodes[0].components[0].type: ...

if (!validation.IsValid)
    return;
```

Validation never stops at the first document problem. `Issues` is emitted in a
deterministic order and each issue has a stable `Code`, `JsonPath` and `Message`.
The current codes are exposed through `SceneValidationCodes` so tools do not
need to parse message text.

The validator checks:

- exact `format` and supported `version`;
- non-empty scene/node names;
- node and component ID grammar and uniqueness;
- parent syntax, missing parents, self-parenting and indirect cycles;
- finite position/rotation/scale and strictly positive scale;
- component type grammar and presence in the explicitly supplied type set.

Passing no registry means the known set is empty. A document containing
components therefore reports `SCN204` until the caller supplies their
descriptors.

## Component registry

`SceneComponentRegistry` is populated explicitly during game setup. It performs
no assembly scanning, reflection-based construction or global service lookup.
Registration is case-sensitive; malformed component types and duplicate
registrations fail immediately.

Implement `ISceneComponentDescriptor` for a reusable descriptor or use
`DelegateSceneComponentDescriptor` for a small game-owned component. A
descriptor receives `SceneComponentValidationContext`, reads its component's
detached JSON `Properties` and calls `Report` for every problem it finds.
`Report` accepts a code, message and optional path relative to `properties`.

The registry borrows its descriptors and owns no disposable resource. Configure
it before parsing/validation work begins and do not mutate it concurrently with
validation. Optional modules remain optional: a game that references one calls
that module's registration method during setup; core Nova3D never discovers or
references the module automatically.

Descriptors that only implement `ISceneComponentDescriptor` participate in
validation but produce no runtime value. Implement
`ISceneRuntimeComponentDescriptor` when a component also needs explicit
`Create` and `Destroy` behavior. Built-in `nova3d.*` descriptors retain the
explicit registration rule.

## Two-phase runtime

Scene loading deliberately separates CPU work from runtime/GPU allocation:

```csharp
SceneLoadPlan plan = SceneLoader.Prepare(
    "Assets/Scenes/level01.scene.json",
    components); // CPU-only: parse, validate and resolve hierarchy

// Run from LoadContent or another graphics-thread-owned point.
var instantiator = new SceneInstantiator();
using SceneInstance instance = instantiator.Instantiate(plan);
```

`SceneLoader.Prepare` throws `SceneDocumentValidationException` with every
semantic issue before any runtime factory runs. Its immutable `SceneLoadPlan`
contains nodes in parent-before-child order plus resolved local and world
matrices. It can be prepared by background CPU work.

Create `SceneInstantiator` on the graphics thread. It captures that thread and
requires both `Instantiate` and the resulting `SceneInstance.Dispose` to run
there. Runtime descriptors may close over explicit dependencies such as a
`GraphicsDevice`, asset manager or physics world; Nova3D does not use a service
locator or discover dependencies globally.

Factories run in deterministic node/component order. If one fails, already
created values are destroyed in reverse order before a contextual
`SceneInstantiationException` is thrown. A successful `SceneInstance` owns all
values returned by runtime descriptors and also destroys them in reverse order.
The registry continues to borrow the descriptor objects themselves.

## Document fields

| Field | Required | Contract |
|---|---:|---|
| `format` | yes | Must be exactly `nova3d.scene`. |
| `version` | yes | Positive integer. G1 supports only `1`. |
| `name` | yes | Non-empty display name; it is not an identifier. |
| `nodes` | yes | Array of zero or more node objects. |

Property names are case-sensitive and use `camelCase`. Unknown properties are
errors in every object owned by the scene format. A registered component owns
and validates the fields inside its `properties` object.

JSON must be UTF-8. Comments, trailing commas, `NaN`, positive/negative
infinity and duplicate JSON properties are invalid. These rules keep the same
document deterministic across the CLI, runtime and agent tooling.

## Nodes and hierarchy

Each node has the following shape:

```json
{
  "id": "player-spawn",
  "name": "Player Spawn",
  "parent": "level-root",
  "transform": {
    "position": [0.0, 1.0, -4.0],
    "rotationDegrees": [0.0, 180.0, 0.0],
    "scale": [1.0, 1.0, 1.0]
  },
  "components": []
}
```

| Field | Required | Contract |
|---|---:|---|
| `id` | yes | Stable, scene-wide unique identifier. |
| `name` | yes | Non-empty display name; duplicates are allowed. |
| `parent` | no | ID of another node. Omit it for a root node. |
| `transform` | yes | Local transform relative to `parent`, or world transform for a root. |
| `components` | yes | Array of component envelopes; it may be empty. |

IDs are compared with ordinal, case-sensitive comparison. They contain 1–128
ASCII characters and match `[A-Za-z][A-Za-z0-9_.-]*`. Renaming a display
`name` does not break references; changing an `id` does.

The node list is flat by design. `parent` creates the hierarchy, which makes
missing references and cycles diagnosable before runtime allocation. A parent
may appear after its child in input. Canonical writers emit parents before
children and preserve sibling order. Array order is stable tooling data, not a
gameplay update-order contract.

The following are structural errors:

- duplicate node IDs;
- a `parent` equal to the node's own ID;
- a `parent` that does not exist;
- any direct or indirect parent cycle.

## Coordinates and transforms

Scene coordinates follow Nova3D/MonoGame conventions:

- right-handed world;
- positive X points right;
- positive Y points up;
- the conventional forward direction is negative Z;
- one world unit represents one metre for physics and authored content.

Games may intentionally use another scale, but reusable Nova3D physics and
camera recipes assume metres. A scene file never performs glTF axis conversion;
the glTF importer remains responsible for imported asset conventions.

`position`, `rotationDegrees` and `scale` are arrays of exactly three finite
JSON numbers:

```text
position        [x, y, z] in world units
rotationDegrees [pitchX, yawY, rollZ] in degrees
scale           [x, y, z], each component greater than zero
```

The local MonoGame matrix is defined exactly as:

```csharp
Matrix.CreateScale(scale)
    * Matrix.CreateFromYawPitchRoll(
        MathHelper.ToRadians(rotationDegrees.Y),
        MathHelper.ToRadians(rotationDegrees.X),
        MathHelper.ToRadians(rotationDegrees.Z))
    * Matrix.CreateTranslation(position);
```

For a child node, `world = local * parentWorld`. Input rotations may contain any
finite degree value. A canonical writer normalizes each value to `[-180, 180)`.
Zero or negative scale is rejected so a scene cannot silently introduce a
singular transform or mirrored winding.

## Components

G1 defines the component envelope, not the runtime component catalog:

```json
{
  "id": "visual",
  "type": "nova3d.model",
  "properties": {
    "asset": "Models/crate.glb"
  }
}
```

| Field | Required | Contract |
|---|---:|---|
| `id` | yes | Unique within its node; follows the node ID grammar. |
| `type` | yes | Registered, version-aware component type. |
| `properties` | yes | JSON object validated by that component descriptor. |

Component types use lower-case dot-separated names, such as `nova3d.model` or
`mygame.enemy`, and match
`[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+`. The `nova3d.*` prefix is reserved for
components shipped by Nova3D. Games and integrations choose their own prefix.

Components are resolved through an explicit registry. There is no assembly
scanning, service locator or type-name reflection. An unknown component type is
an error even when its `properties` object is otherwise valid. Component array
order is preserved but does not imply update order.

## Built-in component catalog

Call `RegisterNova3DBuiltIns` to register camera, directional-light, spawn and
tag descriptors. Pass a `SceneModelComponentDescriptor` to the same call when
the scene uses models:

```csharp
var components = new SceneComponentRegistry();
var assetResolver = new SceneAssetResolver(assetsRoot);
var modelCache = new SceneAssetCache<GltfModel>(
    (path, cancellation) =>
    {
        cancellation.ThrowIfCancellationRequested();
        return new GltfImporter(graphicsDevice).Load(path);
    },
    model => model.Dispose());
var modelDescriptor = SceneModelComponentDescriptor.CreateCached(
    assetResolver,
    modelCache,
    model => new GltfModelRenderer(model, pbrEffect, sun, environment));
components.RegisterNova3DBuiltIns(modelDescriptor);
```

Dependencies remain explicit and no service locator is used. Dispose every
`SceneInstance` before `modelCache`, both on their owning graphics thread.

| Type | Properties | Runtime value |
|---|---|---|
| `nova3d.model` | `asset` required; `visible` and `castShadows` default `true` | `SceneModelComponent` |
| `nova3d.camera` | `primary` default `false`; FOV `45`; near `0.1`; far `1000` | `SceneCameraComponent` |
| `nova3d.directional-light` | color `[1,1,1]`; intensity `1`; `castShadows` true | `SceneDirectionalLightComponent` |
| `nova3d.spawn` | non-empty `kind` | `SceneSpawnPoint` |
| `nova3d.tag` | non-empty `value` | `SceneTag` |

Built-in descriptors reject unknown properties. Model paths must be portable,
relative `.glb`/`.gltf` references. Camera/light orientation follows the node's
negative-Z forward axis; position and all component transforms use the resolved
world matrix. Spawn and tag values contain no gameplay behavior.

`SceneModelComponent` owns its renderer and, by default, the loaded model.
Pass `ownsModels: false` only when the injected asset system retains model
ownership. The scene instance disposes renderers and owned models; do not
dispose component values separately. Physics stays optional: its module or the
game registers an `ISceneRuntimeComponentDescriptor` explicitly in the same
registry, without a core Nova3D-to-BEPU dependency.

## Asset resolution and cache

`SceneAssetResolver` resolves `/`-separated references against one configured
`Assets` root. It rejects absolute paths, URI schemes, backslashes, empty/`.`/`..`
segments, root escapes and casing mismatches. A missing file throws
`SceneAssetNotFoundException` with the scene path, authored reference and full
resolved path; an unsafe reference throws `SceneAssetPathException`.

`SceneAssetCache<T>` is a graphics-thread-affine, reference-counted owner. Its
loader and destroy callbacks define the resource type without hiding MonoGame
objects. `Acquire` returns a `SceneAssetLease<T>`:

- identical resolved paths share one cached value;
- disposing a lease releases one reference but retained caches keep the value;
- `keepUnusedAssets: false` destroys a value after its final lease;
- disposing the cache destroys every retained value exactly once;
- a lease cannot expose a value after either it or its cache is disposed.

Dispose leases/scenes before their cache during normal shutdown. Cache shutdown
with outstanding leases is nevertheless deterministic and late lease disposal
does not destroy the asset twice.

`SceneInstantiator.Instantiate` accepts a `CancellationToken`. It checks before
each factory and immediately after a value is registered for ownership. Runtime
descriptors receive the same token through
`SceneComponentInstantiationContext.CancellationToken`. Cancellation propagates
as `OperationCanceledException` after reverse-order rollback; it is not wrapped
as a component failure. GPU factories remain synchronous on the graphics
thread—the token provides cooperative cancellation, not background GPU work.

## Prefabs (G2.4)

A prefab is another `scene/1` file under the configured `Assets` root. Attach
it to an anchor node through the reserved `nova3d.prefab` component:

```json
{
  "id": "house-a",
  "name": "House A",
  "transform": {
    "position": [10, 0, 0],
    "rotationDegrees": [0, 0, 0],
    "scale": [1, 1, 1]
  },
  "components": [{
    "id": "prefab",
    "type": "nova3d.prefab",
    "properties": {
      "asset": "Prefabs/house.scene.json",
      "overrides": [
        { "node": "body", "component": "tag", "property": "value", "value": "red-house" },
        { "node": "body", "property": "position", "value": [0, 1, 0] }
      ]
    }
  }]
}
```

Use `ScenePrefabLoader.Prepare(scenePath, components, assetResolver)` instead of
`SceneLoader.Prepare` when a document contains prefabs. The loader validates and
expands every referenced file on the CPU, then returns a normal `SceneLoadPlan`
to `SceneInstantiator`. No prefab component remains in the runtime plan.

Every expanded node receives the anchor ID as a prefix: `body` becomes
`house-a.body`; nested prefabs add further prefixes. Prefab roots become
children of their anchor, so their local transforms compose normally. Multiple
uses of one prefab have independent IDs and independent overrides.

An override names a node ID local to the referenced prefab. With `component`,
it replaces an **existing top-level property** of that component, retaining
the property's JSON type and nested shape. Without `component`, `property`
must be `position`, `rotationDegrees` or `scale`, and `value` must be a finite
three-number vector. The modified document is validated again, including
component-specific rules. Unknown targets, duplicate overrides, malformed
fields and recursive asset references fail during CPU preparation. Recursion
raises `ScenePrefabException` with the referencing scene and anchor; no GPU
resource has been allocated.

See [`prefab-demo.scene.json`](Examples/Scenes/prefab-demo.scene.json) for two
instances of the same prefab and one nested prefab. Because prefab references
use a registered component envelope, the structural `scene/1` schema does not
change.

## Scene transitions (G3.1)

`SceneService` owns one active `SceneInstance`. Create it beside a
`SceneInstantiator` on the graphics thread, and dispose it before shared asset
caches. `Load` accepts a CPU-plan factory; `Activate` accepts an already prepared
plan (which may be prepared on another thread); `Reload` re-instantiates the
active plan; `Unload` destroys the active instance.

```csharp
using var scenes = new SceneService(new SceneInstantiator());
scenes.Load(token => ScenePrefabLoader.Prepare(path, components, resolver));
// Later, on the same graphics thread:
scenes.Activate(nextPlan);
```

Preparation or instantiation failure and cancellation retain the previous
scene. Replacement is instantiated before the previous scene is disposed.
Only one transition may mutate the service at a time; reentrant calls fail.
`IProgress<SceneTransitionProgress>` reports discrete stages (Preparing,
Instantiating, Unloading, Complete) without a percentage or UI dependency.
Use a synchronous progress sink if a same-frame ordering guarantee matters:
`Progress<T>` may dispatch callbacks asynchronously. A failure while disposing
the old instance leaves the replacement active and propagates the disposal
exception; the old instance has already attempted destruction. The service does
not own the asset cache, descriptor registry or instantiator.

`Load` deliberately runs its factory on the calling graphics thread. Prepare
CPU work separately when loading off-thread, then call `Activate` on the
graphics thread. Do not instantiate MonoGame resources in background tasks.

## Game flow (G3.2)

`SceneFlowController` borrows a `SceneService` and provides the reusable phases
`Boot`, `Menu`, `Loading`, `Playing`, `Paused` and `Result`. It owns no game
simulation, physics world, UI screen or scene service. Create both on the
graphics thread. The game remains responsible for interpreting victory/defeat,
updating simulation only while appropriate, and mapping each phase to Gum
screens or other UI.

```csharp
var flow = new SceneFlowController(scenes);
flow.StateChanged += (before, after) => UpdateGameUi(after);
flow.ReturnToMenu();
flow.Start(token => ScenePrefabLoader.Prepare(levelPath, components, resolver));
flow.Pause();
flow.Resume();
flow.ShowResult(); // The game decides what the result means.
flow.Restart();    // Re-instantiates the active plan.
flow.ReturnToMenu(); // Disposes the scene; menu UI remains game-owned.
```

`Start` may accept an already prepared plan. Loading is a synchronous,
observable intermediate phase; for background CPU preparation, prepare a plan
separately and call `Start(plan)` on the graphics thread. Failed preparation,
instantiation and cancellation restore the previous phase and retain the old
scene. If destruction of the old scene fails after replacement, the replacement
remains active and the controller settles in `Playing`. `StateChanged` runs
synchronously on the graphics thread; listeners must not start another flow
transition or throw. The controller rejects reentrant transitions. `Restart`
is valid from Playing, Paused or Result and requires an active scene. The
controller borrows the service; dispose the service after returning to menu or
when shutting down the game.

With optional BEPU, use `BepuSceneFlowAdapter` to gate fixed-step updates by
the flow state. A scene runtime descriptor must own and remove every body or
static it creates; the adapter does not dispose physics resources. UI should
observe `StateChanged` and map phases to its own Gum screens. Neither integration
creates a core Nova3D dependency on BEPU or Gum. The headless physics benchmark
repeats menu/phase switches and checks that body/static counts return to zero.

## Debugging and sample

`SceneDebugVisualization.Queue(scene, debugRenderer, nameAnchor)` draws a small
oriented box for every non-model node and a world-space model bounds box for
model nodes. The optional callback receives each node's display name and a
world-space anchor; project it through the active camera and draw text with
your own `SpriteBatch`/`SpriteFont`. Call `DebugRenderer.Flush(renderContext)`
after world geometry. Debug resources belong to the caller and must be disposed
on the graphics thread.

`Samples/DataDrivenScene` loads an external scene with a camera and two
instances of a nested prefab, then draws cube placeholders from instantiated
tags. Press F3 to toggle bounds and projected node names. Production models
use the existing `nova3d.model` descriptor and configured GLB loader/cache.

The project template offers `dotnet new nova3d -n MyGame --scene`. It includes
a smaller JSON scene and two prefab instances. The default template continues
to open its procedural starter scene.

For a visual G3 lifecycle test, run
`dotnet run --project Samples/SceneFlow/SceneFlow.csproj`. Its two external
JSON levels create scene-owned BEPU boxes and statics; Gum menus observe the
flow. Pause/result freeze the falling boxes, restart recreates them, and menu
must report zero bodies/statics. See `Samples/SceneFlow/README.md` for controls.

## Portable asset references

Persisted asset paths inside component properties use these rules when the
component declares a field as an asset reference:

- paths are relative to the game's configured `Assets` root;
- `/` is the persisted separator on every platform;
- absolute paths, drive letters, URI schemes, `.` and `..` segments are invalid;
- resolution must remain inside the configured asset root;
- path casing must match the file so Windows-authored scenes work on Linux;
- a scene stores no checkout-specific or user-specific filesystem path.

For example, a model at `Assets/Models/crate.glb` is stored as
`Models/crate.glb`, regardless of the location of the scene file. MGCB logical
names are component-specific and must be explicitly distinguished from runtime
asset paths by the component descriptor; loaders must not guess.

## Versioning and compatibility

`format` identifies the document family and `version` identifies its contract.
Readers reject unsupported versions before processing nodes. They do not try to
interpret a newer document as version 1.

Compatible additions belong in registered component `properties` only when the
component descriptor explicitly understands them. Changes to scene-owned
objects require a new scene version and a documented migration. Writers always
emit an explicit version and never rely on runtime defaults.

## Ownership and threading boundary

The document and load plan are CPU-only immutable data owned by their caller.
They own no streams, content resources, GPU resources or physics handles. The
disposable runtime instance uses two-phase loading:

1. parse and validate CPU data;
2. resolve and create runtime/GPU resources on the graphics thread.

Failure in phase 1 allocates no runtime/GPU resource. Failure in phase 2 rolls
back only resources created for the incomplete instance. Dispose a successful
instance on the thread that owns its `SceneInstantiator`.

## Deliberately outside scene/1

- gameplay scripts and expressions;
- ECS archetypes or system update order;
- embedded binary assets;
- runtime resource handles;
- physics/UI implementation references;
- hot reload and transition state persisted inside the scene document;
- editor-only layout state.

These capabilities must extend the versioned contract rather than appearing as
undocumented fields.
