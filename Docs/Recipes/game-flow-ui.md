# Game flow, checkpoint, timer and UI

## Use when

Adding menu, playing, pause, victory and defeat without placing gameplay state
inside Gum screens.

## Files

```text
Game/GameState.cs
Game/RunSession.cs
Game/UiOverlay.cs
```

## Implementation

Game-owned state:

```csharp
enum GameState { MainMenu, Playing, Paused, Victory, Defeat }

sealed class RunSession
{
    public TimeSpan Elapsed { get; private set; }
    public Vector3 RespawnPoint { get; private set; }
    public void Update(GameTime time) => Elapsed += time.ElapsedGameTime;
    public void ReachCheckpoint(Vector3 point) => RespawnPoint = point;
}
```

Use simple game-owned volumes for checkpoints and finish lines:

```csharp
if (checkpointBounds.Contains(marble.Position) != ContainmentType.Disjoint)
    session.ReachCheckpoint(checkpointSpawn);
if (finishBounds.Contains(marble.Position) != ContainmentType.Disjoint)
    state = GameState.Victory;
if (marble.Position.Y < defeatHeight)
    state = GameState.Defeat;
```

Create Gum controls once and bind changing values:

```csharp
var timer = new Label();
var timerBinding = new GumValueBinding<int>(seconds =>
    timer.Text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss"));

timerBinding.Set((int)session.Elapsed.TotalSeconds);
```

Map states to screens using `GumUiScreenStack`: normal screens cover previous
screens; pause uses `CoversPrevious = false`. Button handlers request game-owned
transitions such as `StartRun`, `Resume`, `Restart` and `ReturnToMenu`.

Only update `RunSession.Elapsed` while `state == GameState.Playing`.

## Ownership

Gameplay owns state, timer, checkpoint and transition meaning. The screen stack
owns pushed screens. Bindings reference existing controls and own no resources.

## Validate

- timer stops while paused and after victory/defeat;
- respawn uses the latest checkpoint;
- Escape/B backs out through one navigation path;
- HUD stays responsive after resize;
- restarting resets timer, body pose and transient UI without recreating host.

## Common failures

- Gum screen becomes source of gameplay truth: headless tests become impossible;
- controls recreated every frame: allocations and lost focus;
- pause screen covers previous unintentionally: HUD disappears;
- timer uses wall-clock time: pause and deterministic tests break.

