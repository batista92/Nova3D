# UI

Nova3D uses Gum as the candidate optional game-UI backend. The U1 spike lives in
the CityBuilder executable until compatibility, input, resizing and frame cost
are validated. Core `Nova3D` must not reference Gum.

The integration is intended for game menus and HUDs. Debug/editor tooling may
use a different immediate-mode UI in the future.

## U1 constraints

- Pin `Gum.MonoGame`; do not use a floating package version.
- Use Gum controls directly instead of creating `NovaButton`, `NovaLabel` or
  equivalent wrappers.
- Initialize and dispose UI-owned state on the graphics thread.
- Draw UI after the HDR scene has been resolved to the back buffer.
- Validate mouse, keyboard and gamepad independently.
- Report CPU time and managed allocations; do not call them GPU measurements.
- Resize must preserve anchors and usable hit targets.

## Spike controls

The CityBenchmark currently displays the U1 overlay automatically:

- mouse: point and click;
- keyboard: `Tab`/`Shift+Tab` to navigate and `Enter` or `Space` to activate;
- gamepad: D-pad/stick to navigate and the primary action button to activate.

The action button and slider exercise focus and interaction. The top-right
metrics label exercises resize anchoring and reports average Gum update CPU,
draw CPU and managed bytes per frame over 120 frames.

## U2 host

Install the optional package and create one host for the game lifetime:

```csharp
using Nova3D.UI.Gum;

var ui = new GumUiHost(this, new GumUiHostOptions
{
    ScalingMode = GumUiScalingMode.Expand,
    DefaultZoom = 1f,
    InputMode = GumUiInputMode.Overlay
});
```

Call `ui.Update(gameTime)` before routing gameplay input and `ui.Draw()` after
world post-processing. Dispose the host on the graphics thread. The host owns
Gum initialization, input-device registration, resize scaling, diagnostics and
full `Uninitialize`; it does not own gameplay controls individually or wrap
their APIs.

`Overlay` captures pointer input only while a Gum control is under the cursor or
being dragged. `Exclusive` captures mouse, keyboard and gamepad, which is suited
to full-screen menus and modal screens. Read `CapturesMouse`,
`CapturesKeyboard` and `CapturesGamePad` before forwarding input to gameplay.

Scaling policies are `Expand`, `ZoomHeight` and `ZoomWidth`. `DefaultZoom` is an
explicit UI/DPI multiplier and must be positive. Gum automatically reapplies the
chosen policy after a window resize.

## U3 navigation

`GumUiScreen` groups a direct Gum root control with navigation metadata. It does
not replace or wrap the controls inside that root:

```csharp
var root = new Panel();
root.Dock(Gum.Wireframe.Dock.Fill);

var resume = new Button { Text = "Resume" };
root.AddChild(resume);

var pause = new GumUiScreen(root, "Pause")
{
    InitialFocus = resume,
    InputMode = GumUiInputMode.Exclusive,
    CoversPrevious = false
};

screens.Push(pause);
```

After `Push`, `GumUiScreenStack` owns the screen. `Pop`, `Clear` or disposing the
stack detaches and disposes it. Do not reuse or manually dispose a pushed screen.
Only the top screen receives `Update`:

- `CoversPrevious = true` hides the previous screen, suitable for normal menu
  navigation;
- `CoversPrevious = false` leaves it visible without updating it, suitable for
  pause menus and modal dialogs;
- `InitialFocus` restores deterministic keyboard/gamepad navigation on entry;
- the top screen determines the host input mode.

`Enter` and `Leave` are lifecycle hooks for game-specific behavior. Animated
transitions are intentionally not part of the U3 foundation.

The validation overlay provides an options screen, a pause modal, Back/Resume
actions and `Escape` navigation. The pause modal includes the explicit Exit
action because `Escape` is reserved for navigation during the spike.

## U4 HUD

HUD content remains made of direct Gum controls. `GumValueBinding<T>` is a
small change detector: it applies a value only when it differs from the last
one, so labels and bars are updated without rebuilding the visual tree:

```csharp
var healthText = new Label();
var healthBar = new Slider { Minimum = 0, Maximum = 100, IsEnabled = false };
var textBinding = new GumValueBinding<int>(value => healthText.Text = $"HP {value}");
var barBinding = new GumValueBinding<int>(value => healthBar.Value = value);

textBinding.Set(health);
barBinding.Set(health);
```

Use Gum anchors on the existing controls for responsive HUD layout. The host's
scaling policy owns the virtual canvas; HUD code must not cache the initial
back-buffer size.

`GumWorldMarker` projects a `Vector3` with the game's real view, projection and
`Viewport`, then converts the result to the current Gum canvas. It hides the
control behind the camera or outside the viewport by default. Set
`ClampToCanvas` to keep an off-screen indicator at `CanvasMargin`.

`GumNotificationQueue` creates a fixed pool of ordinary Gum `Label` controls at
construction and reuses them. `Enqueue` never creates a control and `Update`
does not allocate. Capacity is explicit; when all slots are active, the next
slot is replaced. The queue does not own the containing screen—the screen stack
continues to own and dispose the visual tree.

The CityBenchmark U4 overlay validates a bottom-left health label/bar,
bottom-right notifications, resize anchors and a marker projected over the
world origin. Its metrics remain CPU/allocation measurements, not GPU timings.

## U5 themes and fonts

`GumUiTheme` is a set of reusable visual tokens for Gum V3 controls. It applies
colors and `GumFontStyle` to existing `Button`, `Label` and `Slider` instances;
it does not create, own, wrap or dispose those controls:

```csharp
var theme = new GumUiTheme
{
    SurfaceColor = new Color(28, 39, 54),
    ForegroundColor = Color.White,
    AccentColor = new Color(64, 190, 255),
    BodyFont = new GumFontStyle(18, family: "Noto Sans"),
    TitleFont = new GumFontStyle(26, family: "Noto Sans", isBold: true)
};

var title = new Label { Text = "City" };
theme.Apply(title, title: true);
```

The helper targets Gum's typed V3 visuals initialized by `GumUiHost`. If a game
replaces a control with a custom visual, it must style that visual directly;
silently guessing child names would make themes brittle. Gum continues to own
font loading, generation and caches. `GumFontStyle` only assigns `FontFamily`,
`FontSize`, bold and italic properties and does not dispose font resources.

Choose a font family that exists on every target system or ship/configure a
font through Gum's font pipeline. The benchmark intentionally uses Gum's
default family so it remains portable while validating size, weight and theme
colors.

## U5 gamepad navigation and accessibility

Gum continues to own directional focus navigation and primary activation. Give
every navigable screen an `InitialFocus`; the screen stack restores it when a
screen is pushed or revealed. The host additionally exposes a single edge-based
back action:

```csharp
ui.Update(gameTime);
if (ui.Navigation.BackPressed)
    screens.Pop();
```

`BackPressed` maps keyboard `Escape` and gamepad `B`/`Back` without firing every
frame while held. `BackPlayer` identifies the gamepad that requested it and is
`null` for keyboard. Keyboard/gamepad options on `GumUiHostOptions` also govern
this action. Do not poll the same buttons again in gameplay while UI captures
that device.

Configure the baseline accessibility policy on the host:

```csharp
Accessibility = new GumUiAccessibilitySettings
{
    TextScale = 1.0f,
    MinimumHitTarget = 44f,
    MinimumContrastRatio = 4.5f,
    ReducedMotion = true
}
```

Passing this policy to `GumUiTheme.Apply` scales text and expands interactive
controls to the minimum target. Call `theme.ValidateAccessibility(policy)` once
after constructing a theme; it checks foreground/surface contrast using relative
luminance. This is intentionally a basic foundation, not a screen-reader API.
`ReducedMotion` is exposed for game-owned transitions and animations; the
current screen stack has no animated transitions, so it already honors it.

The benchmark uses a 44-pixel minimum target, a 4.5:1 contrast requirement and
the same back flow for keyboard and controller. Visual validation must still
cover focus visibility, D-pad/stick movement, `A`, `B`, resize and text clipping.

## U5 project template

Generate a 3D starter with the optional UI integration using:

```powershell
dotnet new nova3d -n MyUiGame --ui
```

The option conditionally adds `Nova3D.UI.Gum` and activates `Game/UiOverlay.cs`.
The generated lifecycle follows the same host contract as the benchmark:
initialize after `base.Initialize`, update before gameplay input, draw after the
world and dispose before world resources. Without `--ui`, neither the package
reference nor UI code is active. `--ui` can be combined with `--physics`.

## UI gate result

The optional UI gate was approved on 2026-09-24. The validation covered the Gum
host lifecycle, rendering after the 3D scene, resize behavior, mouse/keyboard/
gamepad interaction, screen navigation, modal input, responsive HUD elements,
notifications, world markers, themes, basic accessibility and the `--ui`
template workflow.

The CityBenchmark overlay exposes smoothed CPU update/draw time and managed
bytes per frame while running over the full 3D benchmark. These values are
diagnostics rather than GPU measurements and remain visible for future
regression comparisons. Package and template validation additionally built all
four generated variants: default, `--physics`, `--ui`, and `--physics --ui`.

The v0.1 visual presentation is intentionally utilitarian. Visual polish,
custom art direction and richer animation are future product work and were not
gate blockers; functional behavior and architectural isolation were approved.

Gate invariants:

- core `Nova3D` has no Gum dependency;
- the default template has no Gum package or UI source file;
- UI controls remain direct Gum controls;
- lifecycle and GPU-thread ownership are explicit;
- gameplay actions remain game-owned;
- performance diagnostics stay available in the benchmark.
