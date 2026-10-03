# Remappable controls screen

## Use when

The game needs player-editable keyboard, mouse or gamepad bindings. Use direct
Gum controls for the screen; this recipe does not add a UI wrapper to Nova3D.

## Files

```text
Game/InputSetup.cs
Game/ControlsScreen.cs
UserData/controls.json (runtime file under a writable user-data folder)
```

## Implementation

Register actions once, then capture the shipped defaults before loading user
settings. Repeat for each context that has remappable actions:

```csharp
var gameplay = router.CreateContext("gameplay", 0, active: true);
var jump = gameplay.Actions.Add("jump", InputActionKind.Digital,
    InputBinding.Key(Keys.Space), InputBinding.GamePadButtonInput(Buttons.A));
gameplay.Actions.CaptureDefaults();

static JsonElement CaptureBindings(InputActionMap map)
{
    using JsonDocument document = JsonDocument.Parse(InputBindingJson.Serialize(map));
    return document.RootElement.Clone();
}

var paths = new GameDataPaths("MyStudio", "MyGame");
var bindingStore = new VersionedJsonStore<JsonElement>(
    paths.BindingsFile, "mygame.bindings", 1,
    () => CaptureBindings(gameplay.Actions),
    validate: profile =>
        InputBindingJson.Apply(gameplay.Actions, profile.GetRawText()));
bindingStore.Load();
```

Make a Gum button for each binding. When clicked, enter a game-owned `Listening`
state. On the next *new* raw MonoGame key/button press, create an `InputBinding`
and call `gameplay.Actions.ReplaceBindings("jump", candidate)`. While listening,
leave the menu context active and blocking gameplay. Do not poll a held button
that opened the capture dialog as the new binding. Mouse/gamepad alternatives can
be preserved by passing them alongside `candidate`.

After replacement, inspect `gameplay.Actions.FindConflicts()`. It reports
action names and binding indices. Show the conflict in the Gum screen and let
the player choose to keep, retry or revert. Conflicts within a single action
are not reported; contexts are checked independently. Nova3D does not decide
whether two actions may intentionally share a control.
Keep a copy of the prior binding list before replacement so Retry/Revert can
restore it with another `ReplaceBindings` call.

Save only after the player confirms:

```csharp
bindingStore.Save(CaptureBindings(gameplay.Actions));
```

For a “Restore defaults” button, call `gameplay.Actions.RestoreDefaults()`,
refresh the existing Gum labels, then save again. Handle filesystem errors in
the game; never write into the installed `Content` directory. Keep a non-
remappable Back/cancel path while the capture dialog is open.

## Ownership

The game owns the router, action maps, persisted document type, Gum controls and
the choice of conflict policy. `VersionedJsonStore` coordinates files but owns
no action map. The Gum screen stack owns pushed screens.

## Validate

- remap keyboard and gamepad, save, restart and verify both bindings;
- reject a damaged or future-version JSON profile without partial changes;
- restore defaults and verify action object references still work;
- verify a held menu key does not activate gameplay when closing the screen.

## Common failures

- Calling `CaptureDefaults` after loading settings captures player choices as
  defaults.
- Saving every frame creates I/O stalls; save on confirmation only.
- Replacing the `InputAction` object breaks cached references; use
  `ReplaceBindings` on the existing map.
- Assuming conflicts across separate contexts are automatic; inspect each map
  or define a game-specific cross-context policy.
