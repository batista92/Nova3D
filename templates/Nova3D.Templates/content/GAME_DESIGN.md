# Game design brief

Complete this before building menus, HUD and interactive objects. Replace the
starter examples with decisions based on this game's brief and available art.
Read `Docs/Recipes/game-presentation.md` for the implementation and review
contract. Keep this file short so another agent can use it without rediscovering
the intended design.

## Player and world

- Genre and core player action:
- Player fantasy and goal:
- Intended platform, input devices and reference resolution:
- Available models, textures, audio and fonts (with paths):

## Visual direction

- Mood and visual references (describe why each fits):
- Palette: background, surface, primary action, warning and text:
- Type hierarchy: title, section, body, HUD:
- Shape language and spacing:
- World view behind the menu, or deliberate alternative:

## Screens and controls

| Screen | Primary action | Supporting information | Focus/Back behavior |
|---|---|---|---|
| Main menu | | | |
| Settings | | | |
| Pause | | | |
| HUD | | | |
| Victory / defeat | | | |

For each setting, name its data type and fitting control. Continuous values
such as volume use a slider with a visible value; binary values use a toggle.
Record how the chosen value is saved and restored.

## Interactive feedback

| Object | Idle cue | Activation cue | Reduced motion behavior |
|---|---|---|---|
| Pickup | | | |
| Hazard | | | |
| Checkpoint / objective | | | |
| Exit / finish | | | |

## Acceptance

- Which screens and gameplay moments will be captured?
- What must be checked at the reference and smaller window sizes?
- Which input devices can be tested here, and which require a later test?
