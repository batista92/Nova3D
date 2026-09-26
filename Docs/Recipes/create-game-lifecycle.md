# Create a game and preserve lifecycle

## Use when

Starting a new Nova3D game. Choose optional packages at generation time.

## Files

```text
Program.cs
Game/MyGame.cs
Game/World.cs
Game/UiOverlay.cs    only with --ui
```

## Implementation

```powershell
dotnet new nova3d -n MyGame --physics --ui
cd MyGame
dotnet run
```

Keep this order in `MyGame`:

```csharp
protected override void Initialize()
{
    base.Initialize();
    _uiHost = new GumUiHost(this);
}

protected override void Update(GameTime time)
{
    _uiHost.Update(time);
    if (!(_uiHost.CapturesMouse || _uiHost.CapturesKeyboard ||
          _uiHost.CapturesGamePad))
        _world.Update(time, GraphicsDevice.Viewport.AspectRatio);
    base.Update(time);
}

protected override void Draw(GameTime time)
{
    _world.Draw();
    _uiHost.Draw();
    base.Draw(time);
}
```

The generated template already contains the complete conditional version. Add
gameplay beside it rather than moving Gum or BEPU into core Nova3D.

## Ownership

- `MyGame`: MonoGame services and `GumUiHost`.
- `World`: scene, rendering and physics resources.
- UI screen stack: every pushed screen.
- `ContentManager`: everything returned by `Content.Load`.

Dispose UI before world GPU resources during unload.

## Validate

```powershell
dotnet build -c Release
dotnet run
```

Resize the window, activate UI by keyboard/gamepad and exit cleanly. Confirm
the default template has no optional package when its flag is omitted.

## Common failures

- Gum initialized before `base.Initialize`: graphics/input initialization may fail.
- UI drawn before post-processing resolve: UI is altered or disappears.
- gameplay receives captured input: buttons activate while the world moves.
- generated game references repository projects: package isolation is not tested.

