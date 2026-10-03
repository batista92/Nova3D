# Releasing Nova3D

For the current 0.3 candidate, use the [0.3 release checklist](RELEASE_0.3.0.md)
and [compatibility guide](compatibility-0.3.md). The 0.2 record below is
historical and does not imply that 0.3 has been published.
On a clean release commit, `eng/prepare-release.ps1` binds package hashes and
graphics evidence to that commit without publishing or creating a tag.

## Historical 0.2.0 release

Nova3D, its optional physics/UI modules and its project template are independent
packages. `Nova3D.Benchmarks` is a consumer and is not included in them.

## Pre-release checklist

- [x] Add the MIT license.
- [x] Set the author and repository metadata.
- [x] Confirm that the `Nova3D`, `Nova3D.Physics.Bepu`, `Nova3D.UI.Gum` and
  `Nova3D.Templates` package IDs are available before the first NuGet
  publication. Verified against the official NuGet API on 2026-09-26; all four
  returned `404` (not published).
- [x] Keep the Gate #2 benchmark baseline recorded.
- [x] Build the runtime and create all four packages in Release configuration.
- [x] Install the template package in an isolated CLI home.
- [x] Generate and build default, `--physics`, `--ui` and `--physics --ui`
  projects.
- [x] Audit all four `.nupkg` archives for identity, metadata, dependencies,
  required payload and unintended assets or build output.
- [x] Validate Marble3D against only the `0.2.0` packages, publish self-contained
  `win-x64` and smoke the executable from a clean path with spaces. See
  [RELEASE_0.2.0.md](RELEASE_0.2.0.md).
- [ ] Repeat the published executable test on a clean Windows machine or VM
  without the .NET SDK.
- [x] Tag the same `0.2.0` source revision used to create the packages
  (`532eb0986bed9846a6cb64d1c8bc81b84abfdbe1`).
- [x] Create the [GitHub Release](https://github.com/batista92/Nova3D/releases/tag/v0.2.0)
  and attach the four `.nupkg` files plus their SHA-256 manifest.

## Local validation

```powershell
.\eng\validate.ps1
```

The command builds MGCB content, runs the automated regressions, packs and
audits all five packages, installs the CLI package in an isolated tool path and
builds all six template variants using an isolated template hive.
See [validation.md](validation.md) for focused options. Use
`eng/install-template.ps1` only when the template should be installed globally
for interactive development.

## Next coordinated release

`Nova3D.Cli` is now a fifth package with the same version as the core, optional
modules and templates. Before publishing it, repository validation must audit
its `DotnetTool` payload and complete an isolated install plus
`nova3d --version` smoke. Publication to nuget.org remains a release action;
G7.1 only prepares and verifies the package locally. See [cli.md](cli.md).
