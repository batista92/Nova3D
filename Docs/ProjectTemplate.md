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

Template release validation must generate and build both variants. This catches
conditional-source failures and verifies that the optional package is resolvable
without making it a dependency of the default project.

The generated project references the `Nova3D` NuGet package rather than the
CityBuilder repository. The local installer registers `artifacts/packages` as
the `Nova3D-Local` feed. When Nova3D is published, users only need to install
`Nova3D.Templates` from NuGet and the same project template remains valid.
