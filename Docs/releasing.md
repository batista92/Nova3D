# Releasing Nova3D

Nova3D, its optional physics/UI modules and its project template are independent
packages. The CityBuilder benchmark is a consumer and is not included in them.

## Pre-release checklist

- [x] Add the MIT license.
- [x] Set the author and repository metadata.
- [ ] Confirm that the `Nova3D` and `Nova3D.Templates` package IDs are available
  before the first NuGet publication.
- Run the benchmark baseline and record the result.
- Build the runtime and create all four packages in Release configuration.
- Install the template package in an isolated CLI home.
- Generate and build default, `--physics`, `--ui` and `--physics --ui` projects.
- Inspect both `.nupkg` archives for unintended assets or build output.
- Tag the same `0.1.0` source revision used to create the packages.

## Local validation

```powershell
.\eng\validate.ps1
```

The command builds MGCB content, runs the physics regression, packs all four
packages and builds all four template variants using an isolated template hive.
See [validation.md](validation.md) for focused options. Use
`eng/install-template.ps1` only when the template should be installed globally
for interactive development.
