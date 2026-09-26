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

Template release validation must generate and build default, `--physics`,
`--ui` and `--physics --ui` variants. This catches conditional-source failures
and verifies that optional packages remain absent from the default project.

The generated project references the `Nova3D` NuGet package rather than the
CityBuilder repository. The local installer registers `artifacts/packages` as
the `Nova3D-Local` feed. When Nova3D is published, users only need to install
`Nova3D.Templates` from NuGet and the same project template remains valid.
