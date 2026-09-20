# Releasing Nova3D

Nova3D and its project template are independent packages. The CityBuilder
benchmark is a consumer and is not included in either package.

## Pre-release checklist

- Choose and add the project license.
- Replace placeholder author metadata and add the repository URL.
- Confirm that the `Nova3D` and `Nova3D.Templates` package IDs are available.
- Run the benchmark baseline and record the result.
- Build the runtime and create both packages in Release configuration.
- Install the template package in an isolated CLI home.
- Generate a project with `dotnet new nova3d`, then build and run it.
- Inspect both `.nupkg` archives for unintended assets or build output.
- Tag the same `0.1.0` source revision used to create the packages.

## Local validation

```powershell
dotnet build .\CityBuilder.csproj -c Release --no-restore
dotnet pack .\Nova3D\Nova3D.csproj -c Release -o .\artifacts\packages
dotnet pack .\templates\Nova3D.Templates\Nova3D.Templates.csproj `
  -c Release -o .\artifacts\packages
```

Use `eng/install-template.ps1` to install the local packages and verify the
public `dotnet new nova3d -n MyGame` workflow.
