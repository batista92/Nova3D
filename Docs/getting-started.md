# Getting started

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
│   └── World.cs
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
