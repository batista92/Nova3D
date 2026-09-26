# Publish a Windows executable

## Use when

Producing a distributable Windows x64 build after gameplay works in Release.

## Files

```text
MyGame.csproj
Content/Content.mgcb
Assets/**
Shaders/**
```

## Implementation

Validate normal Release first:

```powershell
dotnet build -c Release
dotnet run -c Release
```

Publish a self-contained executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

Output is normally:

```text
bin/Release/net8.0/win-x64/publish/
```

The template already copies runtime `Assets/**` and `Shaders/**`. MGCB places
compiled content under `Content/`. If adding another runtime folder, add an
explicit `CopyToOutputDirectory`/`CopyToPublishDirectory` item in the project.

Create a zip from the contents of `publish/`, not its parent directory. Keep
logs and writable saves outside this directory at runtime.

## Ownership

The game project owns publish metadata, icon, product name and runtime files.
Nova3D packages supply managed/native dependencies through normal NuGet restore.

## Validate

Test the published folder, not `dotnet run`:

1. copy it to a clean path with spaces;
2. start the `.exe` without the repository as current directory;
3. open menu and level;
4. verify shaders, models, textures and audio;
5. verify physics/UI optional modules;
6. create settings/save data and restart;
7. complete victory and defeat flows;
8. inspect logs and exit cleanly.

For release confidence, repeat on a clean Windows machine or VM without the
.NET SDK installed.

## Common failures

- runtime assets referenced relative to repository working directory;
- Content or external GLB images absent from publish output;
- testing only framework-dependent `dotnet run`;
- writing saves into the read-only installation folder;
- zipping the wrong directory level so asset paths change.

