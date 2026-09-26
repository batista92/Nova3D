# Rolling sphere and follow camera

## Use when

Creating a player-controlled dynamic sphere with placeholder box obstacles and
a camera that follows without introducing a scene graph.

## Files

```text
Game/World.cs
Game/MarbleController.cs
Game/FollowCamera.cs
```

## Implementation

Create physics once:

```csharp
_physics = new BepuPhysicsWorld(new PhysicsWorldOptions
{
    Gravity = new Vector3(0f, -18f, 0f),
    FixedTimeStep = 1f / 60f
});
_physics.CreateStaticBox(new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f));
_physics.CreateStaticBox(new Vector3(0f, 1f, -8f), new Vector3(5f, 2f, 1f));
_marble = _physics.CreateDynamicSphere(new Vector3(0f, 2f, 6f), 0.6f, 2f);
```

Gameplay may steer horizontal velocity; BEPU continues to own gravity/contact:

```csharp
Vector2 input = new(
    keyboard.IsKeyDown(Keys.D) ? 1 : keyboard.IsKeyDown(Keys.A) ? -1 : 0,
    keyboard.IsKeyDown(Keys.S) ? 1 : keyboard.IsKeyDown(Keys.W) ? -1 : 0);
if (input.LengthSquared() > 1f) input.Normalize();

Vector3 velocity = _marble.LinearVelocity;
Vector3 target = new(input.X * 9f, velocity.Y, input.Y * 9f);
_marble.LinearVelocity = Vector3.Lerp(velocity, target, 0.12f);
_physics.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
```

Follow after physics and before render-data preparation:

```csharp
Vector3 targetPosition = _marble.Position;
Vector3 wanted = targetPosition + new Vector3(0f, 6f, 10f);
float blend = 1f - MathF.Exp(-8f * (float)gameTime.ElapsedGameTime.TotalSeconds);
_camera.Position = Vector3.Lerp(_camera.Position, wanted, blend);
_camera.Direction = Vector3.Normalize(targetPosition - _camera.Position);
_camera.SetViewport(GraphicsDevice.Viewport);
```

Render the sphere mesh with `_marble.WorldMatrix`; do not duplicate its pose.
Respawn with `_marble.SetPose(spawn, Quaternion.Identity)` and clear velocity.

## Ownership

`BepuPhysicsWorld` owns shapes and simulation. World owns and disposes it.
Rendering owns the sphere mesh independently.

## Validate

- sphere rests on floor and collides with every placeholder;
- controls behave similarly at 30, 60 and 144 FPS;
- camera follows without changing physics pose;
- respawn clears falling velocity;
- Release build reports no steady-state allocation spike from control code.

## Common failures

- multiplying fixed-step internally again: pass frame seconds only to `Update`;
- moving render transform instead of body: visual and collider diverge;
- setting Y velocity to zero while steering: gravity appears broken;
- camera update before physics: visible one-frame lag.

