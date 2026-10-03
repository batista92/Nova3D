# Input actions (G4)

`Nova3D.Production.Input` maps gameplay intentions to MonoGame input states.
It does not replace `KeyboardState`, `MouseState` or `GamePadState`; the game
samples those types directly and may still inspect them for low-level behavior.

## G4.1 action map

Create actions and bindings during setup. Call `Update` once per game frame:

```csharp
using Microsoft.Xna.Framework.Input;
using Nova3D.Production.Input;

var actions = new InputActionMap();
var jump = actions.Add("jump", InputActionKind.Digital,
    InputBinding.Key(Keys.Space),
    InputBinding.GamePadButtonInput(Buttons.A));
var move = actions.Add("move", InputActionKind.Axis2D,
    InputBinding.KeyboardVector(Keys.A, Keys.D, Keys.S, Keys.W),
    InputBinding.GamePadStickInput(InputGamePadStick.Left, deadzone: 0.2f));

// Game.Update, after UI input capture has been considered:
actions.Update(Keyboard.GetState(), Mouse.GetState(), GamePad.GetState(PlayerIndex.One));
if (jump.Pressed) StartJump();
Vector2 direction = move.Value;
```

`Digital` bindings are keys, mouse buttons or gamepad buttons. `Axis1D`
bindings are opposing keys, a gamepad stick/trigger axis, or mouse X/Y/wheel
delta. `Axis2D` bindings are four keys, a gamepad stick, or mouse XY delta.
Bindings must match the action kind; duplicate action names are rejected.
Multiple bindings on an action add their values, except digital actions use
logical OR and expose `Value1D` as 0 or 1.

`Pressed` is true only when an action changes from inactive to active;
`Released` only on active to inactive; `Down` while active. These are action
transitions, not individual binding transitions: switching from Space to a
held mouse button does not release and re-press `jump`. `Value` is `Vector2`;
digital and 1D actions use X and expose `Value1D` for convenience. Axis values
are **not automatically clamped** after combining bindings. A keyboard diagonal
is `(1, 1)` so the game can choose movement normalization; mouse deltas retain
their magnitude for camera controls. A nonzero post-deadzone axis sets `Down`.

Deadzone zeros gamepad-axis magnitudes at or below the configured threshold,
and uses a radial threshold for 2D sticks. `scale` multiplies the result;
`invert` flips its sign (both components on a 2D binding). Deadzone is in
`[0, 1)` and scale must be finite and positive. The first mouse sample has
zero movement/wheel delta. Call `Reset` after focus loss; it clears held states
and the mouse baseline without synthesizing `Released` events.

## G4.2 contexts and UI capture

Use a router when gameplay, debug and menu actions coexist. Higher priority
contexts update first; a blocking context prevents lower contexts from receiving
input. Equal priorities preserve registration order. Activation is explicit:

```csharp
var input = new InputContextRouter();
var gameplay = input.CreateContext("gameplay", priority: 0, active: true);
var debug = input.CreateContext("debug", priority: 10, active: true);
var menu = input.CreateContext("menu", priority: 20,
    blocksLowerContexts: true);
var jump = gameplay.Actions.Add("jump", InputActionKind.Digital,
    InputBinding.Key(Keys.Space));

// On pause/menu entry and exit:
menu.SetActive(isMenuOpen);

// Game.Update: update Gum first, then route the same native input snapshots.
ui.Update(gameTime);
input.Update(Keyboard.GetState(), Mouse.GetState(),
    GamePad.GetState(PlayerIndex.One), GumInputCapture.Read(ui));
if (jump.Pressed) StartJump();
```

`GumInputCapture` is in optional `Nova3D.UI.Gum`; core input has no Gum
dependency. Without Gum, pass `InputDeviceCapture.None` (the default), or
specify device flags explicitly. Capture masks keyboard, mouse and gamepad
independently. When capture changes, an affected action is cleared without a
synthetic `Released` edge. A held input remains suppressed until it goes neutral,
so closing a menu cannot trigger a gameplay action. Contexts newly receiving
input after the first update follow the same neutral-before-press rule. This can
intentionally ignore a key pressed on the exact activation frame; activate the
context before its next input frame. Call `input.Reset()` after focus loss.

## G4.3 remapping and persistence

Create actions during setup, then call `CaptureDefaults()` before applying a
user profile. `ReplaceBindings(name, bindings...)` changes an existing action
without replacing its object, so cached `InputAction` references remain valid.
`RestoreDefaults()` restores the captured setup bindings. Replacement clears
the action state and requires a neutral input sample before another press.

`InputBindingJson.Serialize(map)` writes readable version-1 JSON for one map;
`InputBindingJson.Apply(map, json)` validates the profile version, exact action
set, kinds and all bindings before changing anything. Unknown or damaged data
throws `JsonException` and leaves the map unchanged. The game owns the settings
path and file I/O. Call `FindConflicts()` after a proposed remap: it reports
shared physical controls between distinct actions in the same map, including
keys used by an axis/vector and gamepad stick/axis overlap. It does not reject
conflicts automatically; sharing may be intentional. Contexts are independent.

See [the controls-screen recipe](Recipes/control-settings.md) for Gum capture,
conflict presentation and saving. The headless `Benchmarks/InputContractTests`
regression covers device capture, priority, modal isolation, held-input
transitions, JSON round trips, invalid-profile rollback, conflict detection,
default recovery and zero managed allocations in steady-state updates.

## G4.4 validation sample

Run `dotnet run --project Samples/InputActions/InputActions.csproj`. The sample
uses one gameplay implementation for keyboard and gamepad, displays the active
binding and action count, and exposes a blocking menu context. F2/gamepad Y
changes the pulse action from Space/A to J/B and persists it under the current
user's local application-data folder; F3/gamepad X restores and saves defaults.
Escape/Start toggles the modal, where Enter/A must affect only the menu. The
headless contract test remains the deterministic and allocation regression;
the sample is the visual/device check.
