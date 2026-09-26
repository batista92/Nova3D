# Getting started

This is the shortest path to a running project. Continue with the
[game project guide](game-project-guide.md) for lifecycle, module boundaries,
audio and executable publishing. Use [troubleshooting.md](troubleshooting.md)
when the generated project does not behave as described.

## Install the local template

From the Nova3D repository:

```powershell
.\eng\install-template.ps1
```

The script packs `Nova3D` and `Nova3D.Templates`, registers the local package
feed and installs the `nova3d` template.

## Create a game

```powershell
dotnet new nova3d -n MyGame
cd MyGame
dotnet run
```

The generated project opens a 3D scene containing a rotating cube. Press
`Escape` to close it.

Create the same scene with the optional Gum HUD/menu starter:

```powershell
dotnet new nova3d -n MyUiGame --ui
cd MyUiGame
dotnet run
```

`--ui` adds `Nova3D.UI.Gum`, initializes the UI after MonoGame, draws it after
the 3D scene and disposes it on the graphics thread. Omit the option and the
generated project has no Gum dependency. Options can be combined:

```powershell
dotnet new nova3d -n MyFullGame --physics --ui
```

```text
MyGame/
├── Assets/
│   ├── Audio/
│   ├── Models/
│   └── Textures/
├── Content/
│   └── Content.mgcb
├── Docs/
├── Game/
│   ├── MyGame.cs
│   ├── World.cs
│   └── UiOverlay.cs       # generated with --ui
├── Shaders/
├── AGENTS.md
├── MyGame.csproj
└── Program.cs
```

Use `Content/` for assets compiled by MGCB. Use `Assets/` for runtime files such
as GLB models and their external images. Runtime assets must be copied to the
output directory by the project file.

`MyGame` owns the MonoGame lifecycle. `World` owns scene resources and disposes
them. Nova3D does not replace `Game`, `GraphicsDevice`, `ContentManager`, input,
audio, or platform services.

## Load a GLB

```csharp
using Nova3D.Production.Assets.Gltf;

var model = new GltfImporter(GraphicsDevice)
    .Load("Assets/Models/building.glb");
```

Create GPU resources on the graphics thread and dispose `GltfModel` when its
owner unloads. Prefer GLB over glTF for portable runtime assets and hot reload.

## Validate the installation

```powershell
dotnet new nova3d -h
dotnet build -c Release
```

Template help must show the `--physics` and `--ui` options. A normal Release
build validates compilation and MGCB processing; run the game to validate the
graphics backend and runtime assets.

## Report a Nova3D problem

Copy [NOVA3D_EVALUATION_TEMPLATE.md](NOVA3D_EVALUATION_TEMPLATE.md) to the game
repository. Reproduce the issue with the smallest scene possible and determine
whether the owner is game code, documentation, a Nova3D package or an upstream
dependency before changing toolkit code.
