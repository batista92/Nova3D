# SceneFlow visual sample

From the repository root:

```powershell
dotnet run --project .\Samples\SceneFlow\SceneFlow.csproj
```

For a headless check of both scene files and transitions, add `-- --smoke` to
the command. It creates no window.

The two external JSON scenes are `Assets/Scenes/level-a.scene.json` and
`level-b.scene.json`. The sample-specific `sample.rigid-box` descriptor creates
and removes BEPU bodies/statics; it is not part of Nova3D core. Gum screens
observe `SceneFlowController.StateChanged` and never set the phase directly.

Use the buttons or these shortcuts:

| Key | Action |
|---|---|
| F1 / F2 | Start or switch to Level A / B |
| P | Pause / resume |
| Space | Show result |
| R | Restart current phase |
| M | Return to menu |
| Esc / gamepad B | Pause, resume, return to menu or exit |

Visual check: a level has falling colored boxes. While paused or showing the
result, their positions and the first-body Y value must stop changing. Restart
resets the boxes. Returning to menu must show zero bodies and zero statics;
switching repeatedly between levels must not increase those counts. The title
bar repeats the current phase and physics counts.

This sample deliberately uses simple cube placeholders. It tests scene/physics/UI
lifecycle rather than rendering quality.
