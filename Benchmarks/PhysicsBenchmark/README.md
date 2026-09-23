# Physics Benchmark

Headless foundation test for `Nova3D.Physics.Bepu`. It validates fixed-step
accumulation, gravity, box/sphere/capsule rigid bodies, static collision,
raycasts and body/static removal, then measures 1,000 dynamic boxes for 120
simulation steps. A layer-filtered body must also pass through a ground collider
that does not accept its layer.

The benchmark also builds a 4x4 chunked terrain from a shared height provider,
checks raycast alignment, applies runtime deformation and verifies that only the
affected physical chunks are rebuilt.

Kinematic trigger volumes are checked for enter, stay and exit transitions.
Sphere and capsule casts verify closest-hit distance and optional trigger filtering.
A distance constraint must converge two dynamic bodies to its target distance.
The automated Physics Gate also compares two identical single-thread simulations
bit for bit and measures steady-state allocations with 1,000 bodies. Determinism
is intentionally a local single-thread guarantee; the multithread run remains a
performance test.

```powershell
dotnet run --project .\Benchmarks\PhysicsBenchmark\PhysicsBenchmark.csproj -c Release
```

The interactive half of the gate lives in the CityBenchmark. Run the main
project, press `F5` for physics debug rendering and `F6` to reset its bodies.
