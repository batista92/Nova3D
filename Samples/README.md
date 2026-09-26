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

Controls:

- all samples: `Escape` exits or navigates back;
- `PhysicsPlayground`: `R` rebuilds the sphere stack;
- `GumMenus`: mouse, Tab/Enter or gamepad navigation;
- `RollingBall`: WASD or arrow keys, Escape/B pauses and resumes.

Build all samples as part of the normal regression command:

```powershell
.\eng\validate.ps1
```

The samples are source references, not game presets. Copy the relevant pattern,
then keep gameplay state and content in the consuming game. For short task
instructions, start with [the recipes](../Docs/Recipes/README.md).
