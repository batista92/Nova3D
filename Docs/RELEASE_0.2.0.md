# Nova3D 0.2.0 release evidence

Date: 2026-09-26

## Repository validation

```text
VALIDATION PASS | scope repository | steps 25 | 84.20s
```

This includes Release/MGCB build, the physics regression, all samples, four
packages, package archive auditing and all four isolated template variants.

## External consumer

Marble3D was updated from the three Nova3D `0.1.0` references to:

```text
Nova3D                  0.2.0
Nova3D.Physics.Bepu     0.2.0
Nova3D.UI.Gum           0.2.0
```

It consumed packages from `artifacts/packages`; it did not use project
references or copied Nova3D source.

```text
VALIDATION PASS | scope game | steps 2 | 6.32s
```

## Windows publish smoke

Command shape:

```powershell
dotnet publish .\Marble3D.csproj -c Release -r win-x64 --self-contained true
```

Evidence:

- `Marble3D.deps.json` contains all three Nova3D `0.2.0` identities;
- publish contains the executable, .NET runtime, SDL/OpenAL, compiled Content,
  shaders and the three Nova3D assemblies;
- 225 files, 85,566,705 bytes;
- copied to a clean path containing spaces;
- launched with that path as the working directory;
- process remained alive for five seconds and was then stopped intentionally.

## Validation boundary

This is an automated startup smoke, not a clean-machine certification. It does
not prove operation on a Windows installation without the .NET SDK. Menu,
audio, physics, checkpoint and victory were previously validated interactively
for Gate #5; those full flows were not repeated after the package-version bump.

## Source revision

The four final packages were rebuilt from and embed repository commit:

```text
532eb0986bed9846a6cb64d1c8bc81b84abfdbe1
```

Annotated tag `v0.2.0` points to that commit and was pushed to `origin` on
2026-09-26.

## GitHub Release

The public [Nova3D v0.2.0 release](https://github.com/batista92/Nova3D/releases/tag/v0.2.0)
was published on 2026-09-27 as a stable release with five assets:

- `Nova3D.0.2.0.nupkg`;
- `Nova3D.Physics.Bepu.0.2.0.nupkg`;
- `Nova3D.UI.Gum.0.2.0.nupkg`;
- `Nova3D.Templates.0.2.0.nupkg`;
- `Nova3D-0.2.0-SHA256SUMS.txt`.
