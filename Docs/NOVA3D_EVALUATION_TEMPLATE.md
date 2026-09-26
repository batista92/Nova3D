# Nova3D evaluation log

Copy this file into a consuming game's `Docs/` folder. Use one entry per problem
and keep resolved entries as regression history.

## Environment

```text
Game:
Game commit:
Nova3D version/commit:
Nova3D.Physics.Bepu version:
Nova3D.UI.Gum version:
Build configuration: Release/Debug
OS:
GPU/driver:
MonoGame backend:
Resolution:
```

## Summary

| ID | Area | Severity | Owner | Status |
|---|---|---|---|---|
| N3D-001 | Example | Major | Unknown | Open |

Severity is `Blocker`, `Major`, `Minor` or `Documentation`. Owner is `Game`,
`Documentation`, `Nova3D.Core`, `Nova3D.Physics.Bepu`, `Nova3D.UI.Gum`,
`Nova3D.Templates` or `Upstream`.

## N3D-001 — Short title

```text
Area:
Severity:
Owner:
Status: Open / Reproduced / Fixed / Verified / Won't fix
First seen in:
Regression from:
```

### Scenario

Describe the player action or smallest scene that exposes the problem.

### Expected

State observable correct behavior.

### Actual

State observable incorrect behavior. Attach logs, screenshots and measurements.

### Minimal reproduction

List deterministic steps and remove unrelated gameplay systems.

### Diagnosis

Record evidence. Do not start with a guessed fix.

### Resolution

Link the owning change and regression check. If the fix is in Nova3D, record the
new package version used by the game.

### Verification

Record the build, environment and exact result after retesting the consumer.
