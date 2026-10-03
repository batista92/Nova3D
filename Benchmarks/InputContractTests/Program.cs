using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nova3D.Production.Input;

var map = new InputActionMap();
var jump = map.Add("jump", InputActionKind.Digital,
    InputBinding.Key(Keys.Space), InputBinding.MouseButtonInput(InputMouseButton.Left),
    InputBinding.GamePadButtonInput(Buttons.A));
var moveX = map.Add("move-x", InputActionKind.Axis1D,
    InputBinding.KeyboardAxis(Keys.A, Keys.D),
    InputBinding.GamePadAxisInput(InputGamePadAxis.LeftX, deadzone: 0.2f));
var lookX = map.Add("look-x", InputActionKind.Axis1D,
    InputBinding.MouseAxisInput(InputMouseAxis.X, scale: 0.1f, invert: true));
var scroll = map.Add("scroll", InputActionKind.Axis1D,
    InputBinding.MouseAxisInput(InputMouseAxis.Wheel, scale: 0.01f));
var move = map.Add("move", InputActionKind.Axis2D,
    InputBinding.KeyboardVector(Keys.A, Keys.D, Keys.S, Keys.W),
    InputBinding.GamePadStickInput(InputGamePadStick.Left, deadzone: 0.2f));
var mouseLook = map.Add("mouse-look", InputActionKind.Axis2D,
    InputBinding.MouseDelta(scale: 0.1f));

var none = new KeyboardState();
var emptyMouse = MouseAt(20, 30, 0);
var noPad = new GamePadState();
map.Update(none, emptyMouse, noPad);
Require(!jump.Down && !jump.Pressed && !jump.Released, "neutral first frame");
Require(mouseLook.Value == Vector2.Zero && lookX.Value1D == 0f,
    "first mouse sample has no delta");

map.Update(new KeyboardState(Keys.Space, Keys.D, Keys.W), MouseAt(30, 35, 120), noPad);
Require(jump.Down && jump.Pressed && !jump.Released && jump.Value1D == 1f,
    "keyboard digital press");
Require(moveX.Value1D == 1f && move.Value == new Vector2(1f, 1f),
    "keyboard axes");
Require(MathF.Abs(lookX.Value1D + 1f) < 0.0001f &&
        mouseLook.Value == new Vector2(1f, 0.5f) &&
        MathF.Abs(scroll.Value1D - 1.2f) < 0.0001f,
    "mouse delta, wheel, scale and inversion");

map.Update(new KeyboardState(Keys.Space, Keys.A, Keys.D), MouseAt(30, 35, 120), noPad);
Require(jump.Down && !jump.Pressed && !jump.Released, "held digital state");
Require(moveX.Value1D == 0f && move.Value == Vector2.Zero,
    "opposing keyboard directions cancel");

map.Update(none, MouseAt(30, 35, 120, ButtonState.Pressed), noPad);
Require(jump.Down && !jump.Pressed && !jump.Released,
    "alternative mouse binding keeps action held");
map.Update(none, MouseAt(30, 35, 120), noPad);
Require(!jump.Down && jump.Released && !jump.Pressed, "digital release");

var smallStick = Pad(Buttons.None, new Vector2(0.1f, 0f));
map.Update(none, emptyMouse, smallStick);
Require(moveX.Value1D == 0f && move.Value == Vector2.Zero, "gamepad deadzone");
var activePad = Pad(Buttons.A, new Vector2(0.75f, -0.5f));
map.Update(none, emptyMouse, activePad);
Require(jump.Pressed && moveX.Value1D > 0.74f &&
        move.Value == new Vector2(0.75f, -0.5f), "gamepad button and stick");

map.Reset();
Require(!jump.Down && !jump.Released && move.Value == Vector2.Zero,
    "reset clears held states");
map.Update(none, MouseAt(100, 200, 500), noPad);
Require(mouseLook.Value == Vector2.Zero && scroll.Value1D == 0f,
    "reset clears mouse baseline");

Expect<InvalidOperationException>(() => map.Add("jump", InputActionKind.Digital),
    "duplicate action");
Expect<ArgumentException>(() => map.Add("wrong", InputActionKind.Digital,
    InputBinding.KeyboardAxis(Keys.A, Keys.D)), "mismatched binding");
Expect<ArgumentOutOfRangeException>(() => InputBinding.GamePadStickInput(
    InputGamePadStick.Left, deadzone: 1f), "invalid deadzone");
Expect<ArgumentOutOfRangeException>(() => InputBinding.MouseDelta(scale: 0f),
    "invalid scale");

// Warm up JIT and native structs before measuring the hot path.
for (var index = 0; index < 1000; index++) map.Update(none, emptyMouse, noPad);
var before = GC.GetAllocatedBytesForCurrentThread();
for (var index = 0; index < 10000; index++) map.Update(none, emptyMouse, noPad);
var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
Require(allocated == 0, $"action map allocated {allocated} bytes in 10k updates");

var captureMap = new InputActionMap();
var capturedJump = captureMap.Add("jump", InputActionKind.Digital, InputBinding.Key(Keys.Space));
captureMap.Update(none, emptyMouse, noPad);
captureMap.Update(new KeyboardState(Keys.Space), emptyMouse, noPad);
Require(capturedJump.Pressed, "uncaptured keyboard reaches action");
captureMap.Update(new KeyboardState(Keys.Space), emptyMouse, noPad, InputDeviceCapture.Keyboard);
Require(!capturedJump.Down && !capturedJump.Released, "UI capture clears without release edge");
captureMap.Update(new KeyboardState(Keys.Space), emptyMouse, noPad);
Require(!capturedJump.Down && !capturedJump.Pressed, "held key cannot leak after capture ends");
captureMap.Update(none, emptyMouse, noPad);
captureMap.Update(new KeyboardState(Keys.Space), emptyMouse, noPad);
Require(capturedJump.Pressed, "new press works after neutral sample");

var router = new InputContextRouter();
var gameplay = router.CreateContext("gameplay", 0, active: true);
var debug = router.CreateContext("debug", 5, active: true);
var menu = router.CreateContext("menu", 10, blocksLowerContexts: true);
var fire = gameplay.Actions.Add("fire", InputActionKind.Digital, InputBinding.Key(Keys.Space));
var toggle = debug.Actions.Add("toggle", InputActionKind.Digital, InputBinding.Key(Keys.F3));
var select = menu.Actions.Add("select", InputActionKind.Digital, InputBinding.Key(Keys.Space));
Require(router.Contexts[0] == menu && router.Contexts[1] == debug &&
        router.Contexts[2] == gameplay, "deterministic priority order");
router.Update(none, emptyMouse, noPad);
router.Update(new KeyboardState(Keys.Space, Keys.F3), emptyMouse, noPad);
Require(fire.Pressed && toggle.Pressed && !select.Down,
    "active nonblocking contexts receive input");
menu.SetActive(true);
router.Update(new KeyboardState(Keys.Space, Keys.F3), emptyMouse, noPad);
Require(menu.IsReceivingInput && !gameplay.IsReceivingInput &&
        !debug.IsReceivingInput && !fire.Down && !toggle.Down && !select.Pressed,
    "modal blocks lower contexts and held input on entry");
router.Update(none, emptyMouse, noPad);
router.Update(new KeyboardState(Keys.Space), emptyMouse, noPad);
Require(select.Pressed && !fire.Down, "modal action works after neutral sample");
menu.SetActive(false);
router.Update(new KeyboardState(Keys.Space), emptyMouse, noPad);
Require(!fire.Down && !fire.Pressed, "held modal key cannot leak to gameplay");
router.Update(none, emptyMouse, noPad);
router.Update(new KeyboardState(Keys.Space), emptyMouse, noPad);
Require(fire.Pressed, "gameplay resumes on fresh press");
Expect<InvalidOperationException>(() => router.CreateContext("menu", 99),
    "duplicate context");
Expect<ArgumentOutOfRangeException>(() => router.Update(none, emptyMouse, noPad,
    (InputDeviceCapture)8), "unknown capture flags");

for (var index = 0; index < 1000; index++) router.Update(none, emptyMouse, noPad);
before = GC.GetAllocatedBytesForCurrentThread();
for (var index = 0; index < 10000; index++) router.Update(none, emptyMouse, noPad);
allocated = GC.GetAllocatedBytesForCurrentThread() - before;
Require(allocated == 0, $"context router allocated {allocated} bytes in 10k updates");

var remap = new InputActionMap();
var remapJump = remap.Add("jump", InputActionKind.Digital,
    InputBinding.Key(Keys.Space), InputBinding.GamePadButtonInput(Buttons.A));
remap.Add("interact", InputActionKind.Digital, InputBinding.Key(Keys.E));
remap.Add("move", InputActionKind.Axis2D,
    InputBinding.KeyboardVector(Keys.A, Keys.D, Keys.S, Keys.W),
    InputBinding.GamePadStickInput(InputGamePadStick.Left));
remap.CaptureDefaults();
var originalJson = InputBindingJson.Serialize(remap);
Require(originalJson.Contains("\"version\": 1") &&
        originalJson.Contains("\"type\": \"GamePadStick\""), "versioned readable JSON");
remap.ReplaceBindings("jump", InputBinding.Key(Keys.E));
var conflict = remap.FindConflicts();
Require(conflict.Count == 1 && conflict[0].FirstAction == "jump" &&
        conflict[0].SecondAction == "interact", "shared key conflict");
remap.ReplaceBindings("jump", InputBinding.Key(Keys.W));
Require(remap.FindConflicts().Count == 1, "vector versus digital key conflict");
remap.ReplaceBindings("jump", InputBinding.GamePadButtonInput(Buttons.A));
remap.ReplaceBindings("interact", InputBinding.GamePadButtonInput(Buttons.A));
Require(remap.FindConflicts().Count == 1, "gamepad button conflict");
var axisMap = new InputActionMap();
axisMap.Add("strafe", InputActionKind.Axis1D,
    InputBinding.GamePadAxisInput(InputGamePadAxis.LeftX));
axisMap.Add("walk", InputActionKind.Axis2D,
    InputBinding.GamePadStickInput(InputGamePadStick.Left));
Require(axisMap.FindConflicts().Count == 1, "gamepad stick versus axis conflict");
var mouseMap = new InputActionMap();
mouseMap.Add("look", InputActionKind.Axis2D, InputBinding.MouseDelta());
mouseMap.Add("zoom", InputActionKind.Axis1D,
    InputBinding.MouseAxisInput(InputMouseAxis.Wheel));
Require(mouseMap.FindConflicts().Count == 0, "mouse wheel does not overlap XY delta");
mouseMap.Add("look-x", InputActionKind.Axis1D,
    InputBinding.MouseAxisInput(InputMouseAxis.X));
Require(mouseMap.FindConflicts().Count == 1, "mouse X overlaps XY delta");
remap.RestoreDefaults();
Require(remap.FindConflicts().Count == 0 && remapJump.Bindings.Count == 2,
    "recoverable defaults preserve action reference");
remap.ReplaceBindings("jump", InputBinding.Key(Keys.F));
InputBindingJson.Apply(remap, originalJson);
Require(remapJump.Bindings.Count == 2, "JSON restores all bindings");
Require(InputBindingJson.Serialize(remap) == originalJson,
    "JSON round trip preserves every device binding");
remap.Update(none, emptyMouse, noPad);
remap.Update(new KeyboardState(Keys.Space), emptyMouse, noPad);
Require(remapJump.Pressed, "restored keyboard binding works");
var beforeBad = InputBindingJson.Serialize(remap);
Expect<System.Text.Json.JsonException>(() => InputBindingJson.Apply(remap,
    originalJson.Replace("\"version\": 1", "\"version\": 99")), "reject unknown profile version");
Expect<System.Text.Json.JsonException>(() => InputBindingJson.Apply(remap,
    originalJson.Replace("\"type\": \"GamePadStick\"", "\"type\": \"Unknown\"")),
    "reject malformed binding transactionally");
Require(InputBindingJson.Serialize(remap) == beforeBad, "failed import leaves bindings intact");
Expect<ArgumentException>(() => remap.ReplaceBindings("jump", InputBinding.MouseDelta()),
    "reject mismatched remap without clearing old bindings");
Require(InputBindingJson.Serialize(remap) == beforeBad, "failed remap leaves bindings intact");
Console.WriteLine("Input G4.3 PASS | JSON | conflicts | defaults | transactional import | 0 B/update");

static MouseState MouseAt(int x, int y, int wheel, ButtonState left = ButtonState.Released) =>
    new(x, y, wheel, left, ButtonState.Released, ButtonState.Released,
        ButtonState.Released, ButtonState.Released);

static GamePadState Pad(Buttons button, Vector2 left) =>
    new(new GamePadThumbSticks(left, Vector2.Zero),
        new GamePadTriggers(0f, 0f), new GamePadButtons(button), new GamePadDPad());

static void Require(bool condition, string evidence)
{
    if (!condition) throw new InvalidOperationException($"Input regression failed: {evidence}.");
}

static void Expect<T>(Action action, string evidence) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}: {evidence}.");
}
