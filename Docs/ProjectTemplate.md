# Nova3D project template

Install the local development package and template:

```powershell
.\eng\install-template.ps1
```

Create and run a game:

```powershell
dotnet new nova3d -n MyGame
cd MyGame
dotnet run
```

The generated project references the `Nova3D` NuGet package rather than the
CityBuilder repository. The local installer registers `artifacts/packages` as
the `Nova3D-Local` feed. When Nova3D is published, users only need to install
`Nova3D.Templates` from NuGet and the same project template remains valid.
