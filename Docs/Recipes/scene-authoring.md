# Author and validate a scene document

## Use when

Use this recipe when an agent or tool must create or modify a versioned
`nova3d.scene` file. G2.1 supports generic two-phase instantiation for explicitly
registered runtime descriptors. Built-in model, camera, light and physics
components arrive in later G2 milestones.

Read [scene format](../scenes.md) for the complete contract. Editors may map
`*.scene.json` to
[`nova3d.scene.1.schema.json`](../Schemas/nova3d.scene.1.schema.json).

## Files

```text
Assets/
└── Scenes/
    └── level01.scene.json
Game/
└── SceneComponents.cs
```

Asset fields inside component properties use paths relative to `Assets/`, such
as `Models/crate.glb`; never store an absolute checkout path.

## Scene document

```json
{
  "format": "nova3d.scene",
  "version": 1,
  "name": "Level 01",
  "nodes": [
    {
      "id": "player-spawn",
      "name": "Player Spawn",
      "transform": {
        "position": [0.0, 1.0, -4.0],
        "rotationDegrees": [0.0, 180.0, 0.0],
        "scale": [1.0, 1.0, 1.0]
      },
      "components": [
        {
          "id": "spawn",
          "type": "mygame.spawn",
          "properties": {
            "kind": "player"
          }
        }
      ]
    }
  ]
}
```

## Implementation

Register component types explicitly and validate before runtime allocation:

```csharp
using System.Text.Json;
using Nova3D.Production.Scenes;

var components = new SceneComponentRegistry();
components.Register(new DelegateSceneComponentDescriptor("mygame.spawn", context =>
{
    if (!context.Component.Properties.TryGetProperty("kind", out JsonElement kind) ||
        kind.ValueKind != JsonValueKind.String ||
        string.IsNullOrWhiteSpace(kind.GetString()))
    {
        context.Report("GAME001", "Spawn kind is required.", "kind");
    }
}));

SceneDocument scene = SceneDocumentSerializer.Load(
    "Assets/Scenes/level01.scene.json");
SceneValidationResult result = SceneDocumentValidator.Validate(scene, components);

if (!result.IsValid)
{
    foreach (SceneValidationIssue issue in result.Issues)
        Console.Error.WriteLine(issue);
    throw new InvalidDataException("Scene validation failed.");
}
```

For runtime descriptors, replace the separate load/validate calls with a plan,
then instantiate it from the graphics thread:

```csharp
SceneLoadPlan plan = SceneLoader.Prepare(
    "Assets/Scenes/level01.scene.json", components);
using SceneInstance instance = new SceneInstantiator().Instantiate(plan);
```

Use `Serialize` or `Save` after editing a `SceneDocument`. The writer preserves
node/component order and normalizes rotations to `[-180, 180)`.

## Ownership

- `SceneDocument` and validation results are immutable CPU data; no disposal.
- `SceneComponentRegistry` is game-owned setup data and borrows descriptors.
- `SceneComponentValidationContext` exists only during a descriptor call; do
  not retain it.
- `SceneLoadPlan` is immutable CPU data and owns no runtime resources.
- `SceneInstance` owns created values and must be disposed on its instantiator's
  thread. Failure rolls back already-created values in reverse order.

## Validate

In a Nova3D source checkout:

```powershell
dotnet run --project .\Benchmarks\SceneContractTests\SceneContractTests.csproj -c Release
```

Completion checks:

- parsing reports the source file and JSON path on malformed input;
- validation returns every semantic issue in deterministic order;
- all used component types are explicitly registered;
- no asset path is absolute or escapes the `Assets` root;
- no GPU resource is created before `IsValid` is true.

## Common failures

| Symptom | Cause / correction |
|---|---|
| `SCN001` or `SCN002` | Use `format: nova3d.scene` and `version: 1`. |
| `SCN102` | Give every node a scene-wide unique ID. |
| `SCN105` | Fix the `parent` ID or add the referenced node. |
| `SCN107` | Break the reported parent cycle. |
| `SCN114` | Every scale component must be greater than zero. |
| `SCN204` | Register the component descriptor before validation. |
| component-specific code | Correct the reported path under `properties`. |

JSON Schema catches local shape errors, but it cannot prove unique IDs, parent
existence, acyclic hierarchy or registered component semantics. Always run
`SceneDocumentValidator` after schema validation.
