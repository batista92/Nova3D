# Nova3D project template

Install the local development package and template:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\install-template.ps1
```

The execution-policy override applies only to this PowerShell process.

Create and run a game:

```powershell
dotnet new nova3d -n MyGame
cd MyGame
dotnet run
```

Create the starter with the optional BEPU physics module and a falling-box
scene:

```powershell
dotnet new nova3d -n MyPhysicsGame --physics
cd MyPhysicsGame
dotnet run
```

Without `--physics`, the generated project has no BEPU dependency. With the
option enabled, it references `Nova3D.Physics.Bepu` and demonstrates the fixed
timestep while keeping rendering and simulation separate.

Create the starter with the optional Gum UI module and overlay:

```powershell
dotnet new nova3d -n MyUiGame --ui
cd MyUiGame
dotnet run
```

The UI variant references `Nova3D.UI.Gum`, creates `GumUiHost` after MonoGame
initialization, supplies a focused button and draws after the 3D world. Without
`--ui`, the generated project has no Gum dependency. Both modules can be used
together with `--physics --ui`.

Add an editable JSON scene with two instances of one prefab:

```powershell
dotnet new nova3d -n MySceneGame --scene
cd MySceneGame
dotnet run
```

The scene variant reads `Assets/Scenes/starter.scene.json` and
`Assets/Prefabs/marker.scene.json`, prepares the hierarchy before runtime
allocation and renders placeholder cubes for tagged nodes. The same option can
be combined with `--physics` and `--ui`. The default template does not include
these scene files.

Template release validation generates and builds default, `--physics`,
`--ui`, `--physics --ui`, `--scene` and fully combined variants. This catches
conditional-source failures and verifies that optional packages remain absent
from the default project.

The generated project references the `Nova3D` NuGet package rather than the
Nova3D source repository. The local installer registers `artifacts/packages` as
the `Nova3D-Local` feed. When Nova3D is published, users only need to install
`Nova3D.Templates` from NuGet and the same project template remains valid.

Every generated project also includes `GAME_DESIGN.md` and
`PRESENTATION_REVIEW.md`. An AI agent should fill the first from the game's brief
and assets before composing UI, then inspect real screenshots and complete the
second before presenting the game as finished. The bundled
`Docs/Recipes/game-presentation.md` gives the short design and validation
contract. The optional Gum overlay demonstrates lifecycle only.
