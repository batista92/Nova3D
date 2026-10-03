# Game presentation contract for AI agents

## Use when

Creating or reviewing a complete game, menu, settings screen, HUD or interactive
world object. This is a game design checklist, not a new Nova3D runtime API.
Generated games include `GAME_DESIGN.md` and `PRESENTATION_REVIEW.md`; fill them
as the game develops. `Game/UiOverlay.cs` in the template only demonstrates Gum
lifecycle and must not be copied as a final design.

## Copy into the game brief or agent task

> Build a playable game with a coherent visual and interaction style. Before
> coding, inspect the brief and available assets. Write a short art direction:
> genre, player fantasy, palette, typography, camera, shape language and how
> menus connect to the world. Use that direction for the menu, HUD, settings,
> pickups and results. Choose controls by meaning: continuous values need
> sliders with visible values; binary settings need toggles; actions need
> buttons. Every interactive object needs a readable idle state, feedback on
> approach or focus where useful, and clear feedback when activated. Animate
> important pickups with restrained rotation or bobbing, and stop or reduce
> nonessential motion when reduced motion is enabled. Make keyboard, mouse and
> gamepad navigation clear. Capture every screen and a representative gameplay
> view, inspect them at the target resolution and one smaller resolution, then
> revise any generic, clipped, inconsistent or hard-to-read result. Report the
> visual decisions and the checks performed; do not call a functional screen
> visually complete without inspecting it.

## Design before implementation

Record five decisions in the game repository before building screens:

1. The game's theme and a small palette with named roles: background, surface,
   primary action, warning and text. Derive them from the game or its assets.
2. A menu composition: background or world view, title, primary action,
   secondary actions and focus state. Marble3D and Relay Runner should not
   receive the same composition just because they use the same Gum controls.
3. A type and spacing hierarchy: title, section, body and HUD. Reserve space
   for translated or longer labels even when the first build is English-only.
4. A control map: volume as 0–100% sliders with numeric readout, mute as a
   toggle, remapping as a listening dialog with cancel and conflict feedback,
   navigation as buttons. If a slider is unavailable, implement a real
   continuous selector with direct manipulation and gamepad/keyboard steps;
   do not substitute a button that cycles four preset values without saying so.
5. An interaction map for collectibles, hazards, checkpoint and exit: idle cue,
   proximity cue if needed, activation cue and reduced-motion behavior. A coin
   can rotate and bob subtly, then play sound/flash and disappear when collected.

Use direct Gum controls and game-owned state. Keep animation time in gameplay
or presentation state; do not recreate controls or models every frame. The
existing `game-flow-ui.md` and `control-settings.md` recipes cover lifecycle
and persistence; this recipe covers the player's experience.

## Review before calling a game complete

- Capture main menu, settings, pause, HUD, victory and defeat. Inspect actual
  rendered images, including a smaller window size, rather than only reading
  the control tree or compiling.
- Navigate every screen with mouse, keyboard and gamepad. Initial focus and
  visible focus must agree with visual order; Back/Cancel must be predictable.
- Change a slider from minimum to maximum using each input method. Confirm the
  value displayed, sound level changed, and choice survives restart.
- Check readable text over the scene, consistent spacing, unclipped controls,
  and a clear primary action. Test settings with music playing so volume is
  meaningful.
- Watch a collectible from a typical camera distance. Verify it stands out,
  moves smoothly, gives collection feedback and does not distract excessively.
  Repeat with reduced motion enabled.
- Record screenshots, observations, fixes and any untested device. A build,
  successful input event or CLI validation alone does not pass this review.

The acceptance check is evidence based: a second agent should be able to open
the brief, screenshots and review record and explain why each control, screen
and pickup belongs to this specific game. If the same composition and palette
could be pasted into an unrelated game unchanged, revise the design.

The [Xbox Accessibility Guidelines](https://learn.microsoft.com/en-us/gaming/accessibility/guidelines)
provide an external reference for navigation, focus, readable UI and motion
preferences. They do not prescribe one visual style for every game.
