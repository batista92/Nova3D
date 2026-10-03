# Nova3D samples

These projects are small, compilable references for game code. They use the
public Nova3D APIs directly and intentionally avoid a shared scene framework.
`Shared/SampleMeshFactory.cs` only removes repeated placeholder mesh data.

| Sample | Demonstrates | Run |
|---|---|---|
| `Minimal3D` | MonoGame lifecycle, `Camera3D`, `Mesh`, `BasicEffect` | `dotnet run --project Samples/Minimal3D/Minimal3D.csproj` |
| `PhysicsPlayground` | fixed-step world, bodies, static floor, pose rendering | `dotnet run --project Samples/PhysicsPlayground/PhysicsPlayground.csproj` |
| `GumMenus` | host, screen stack, direct Gum controls, navigation | `dotnet run --project Samples/GumMenus/GumMenus.csproj` |
| `RollingBall` | physics player, follow camera, obstacle course, checkpoint, timer, pause and end states | `dotnet run --project Samples/RollingBall/RollingBall.csproj` |
| `InputActions` | keyboard/gamepad actions, contexts, modal isolation and atomic versioned remapping | `dotnet run --project Samples/InputActions/InputActions.csproj` |
| `AnimatedCharacter` | external animated GLB, PBR skinning, clip cross-fade, animated shadows, bounds and skeleton debug | `dotnet run --project Samples/AnimatedCharacter/AnimatedCharacter.csproj` |

Controls:

- all samples: `Escape` exits or navigates back;
- `PhysicsPlayground`: `R` rebuilds the sphere stack;
- `GumMenus`: mouse, Tab/Enter or gamepad navigation;
- `RollingBall`: WASD or arrow keys, Escape/B pauses and resumes.
- `InputActions`: WASD/arrows or left stick moves; Space/A activates; Esc/Start
  toggles the modal; F2/Y remaps and saves; F3/X restores defaults.
- `AnimatedCharacter`: fetch the skinning fixtures first; Tab or 1-9 changes
  clips with a cross-fade and D toggles skeleton/bounds debug.

Build all samples as part of the normal regression command:

```powershell
.\eng\validate.ps1
```

The samples are source references, not game presets. Copy the relevant pattern,
then keep gameplay state and content in the consuming game. For short task
instructions, start with [the recipes](../Docs/Recipes/README.md).
