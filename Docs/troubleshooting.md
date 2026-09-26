# Troubleshooting

Use this page before changing rendering constants or adding a workaround.

## Template is missing or outdated

From the Nova3D repository, reinstall the local packages and template:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\install-template.ps1
dotnet new nova3d -h
```

The help output must list `--physics` and `--ui`. Existing generated projects do
not change when the template is reinstalled.

## Package restore cannot find Nova3D

List configured sources:

```powershell
dotnet nuget list source
```

For local development, `artifacts/packages` must be registered as the
`Nova3D-Local` source by the installer. Repack after source changes; a generated
game consumes `.nupkg` files, not the repository projects.

## Window opens and immediately closes

Run from a terminal with `dotnet run` and inspect the exception. Also inspect
the game's log directory. Common causes are missing compiled content, a shader
compiled for the wrong backend, missing runtime GLB images, or initializing Gum
before MonoGame graphics initialization.

## Black or completely bright scene

Check pass order and render targets before tuning exposure:

1. confirm the world pass writes to the intended HDR target;
2. confirm post-processing resolves to the back buffer;
3. confirm UI draws only after that resolve;
4. disable bloom/FXAA individually;
5. inspect shader parameter bindings and color-space conversions.

Do not compensate for a matrix, normal or render-target bug with lighting
constants.

## Missing or partial shadows

Use CSM debug output first. Verify cascade selection, CPU/GPU coordinate
agreement, light view-projection matrices and receiver depth. Do not increase
shadow distance or bias until the debug modes explain the failure.

## Models are invisible or inside-out

Verify that the model uses supported glTF `TRIANGLES`, contains `POSITION`, and
does not require unsupported skinning, animation, morph, Draco or Meshopt
features. Then inspect world transform, bounds, winding/cull state and normal
inverse-transpose handling.

## Physics object falls through geometry

Check that the collider exists, the collision masks accept each other, the
shape dimensions match rendering and physics uses its fixed timestep. For
terrain, visual and physical chunks must share the same height provider and
the affected collider chunks must be rebuilt after deformation.

## Gum controls do not receive input

Confirm `GumUiHost.Update` runs before gameplay input, the required keyboard or
gamepads are enabled, the screen has `InitialFocus`, and gameplay does not also
consume a device captured by an exclusive screen. Draw order does not fix input
routing.

## Performance regressed

Record milliseconds and pass statistics, not FPS alone. Compare the same
Release build, backend, resolution, camera and content. Separate update, shadow,
world, post-processing, physics and UI CPU measurements. Check allocations and
draw/triangle counts before optimizing.

## Information required in a report

- minimal reproduction steps;
- expected and actual behavior;
- Nova3D/package versions and commit;
- OS, GPU, graphics backend and resolution;
- Release or Debug configuration;
- logs/exceptions and screenshot when relevant;
- CPU timings, allocations and render statistics for performance reports;
- workaround, if one exists.

