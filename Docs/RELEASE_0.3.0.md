# Nova3D 0.3.0 release checklist

Status: [GitHub Release v0.3.0](https://github.com/batista92/Nova3D/releases/tag/v0.3.0)
published from commit `018f53de21cf7111f3417d6a4c163c5bafc9010d`.
NuGet publication is deferred by project decision.

After committing the candidate, run `eng/prepare-release.ps1` on the graphics
machine. It refuses a dirty tree or an existing `v0.3.0` tag, runs repository,
visual and performance validation, verifies all five package hashes and writes
`artifacts/packages/RELEASE_PROVENANCE.json` with the source commit. It does
not publish or tag. Keep that provenance file with the release evidence.

## Candidate contents

- Core `Nova3D`: versioned scene/prefab runtime, input, audio, persistence,
  animated glTF and visual-capture contracts.
- Optional `Nova3D.Physics.Bepu` and `Nova3D.UI.Gum`: scene-flow and input
  integration, respectively.
- `Nova3D.Cli`: distributed `doctor`, `validate`, `inspect`, `publish`, visual
  comparison and performance diagnostics.
- `Nova3D.Templates`: coordinated core/optional references, scene variant,
  agent guidance and presentation brief/review files.

See [compatibility-0.3.md](compatibility-0.3.md) for the upgrade and document
format contracts. The [repository changelog](https://github.com/batista92/Nova3D/blob/main/CHANGELOG.md)
records the full candidate.

## Automated evidence

- Local candidate check on 2026-10-03 (uncommitted working tree): repository
  validation passed all 50 steps; all three visual scenes matched their
  baselines with zero differing pixels. On Windows 10/DesktopGL with an
  NVIDIA GeForce GTX 1660 SUPER at 1280x720, the CityBenchmark report passed
  all 11 budget limits over 240 sampled frames (frame interval p95 3.505 ms,
  449 draws, 61,392 triangles). The report and captures are under `artifacts/`
  and are local evidence only; repeat on the release commit.
- [x] Windows CI `eng/validate.ps1` passes on the release revision: build,
  shader probes, contracts, samples, isolated CLI, six template variants and
  archive audit.
- [x] Linux CI core build and CPU contracts pass on the same revision.
- [x] Controlled-machine visual suite and performance gate pass; attach their
  reports and identify hardware/backend. CI's CPU tests are not GPU evidence.
- Release-commit preflight passed 51 repository steps, three exact visual
  comparisons and all 11 CityBenchmark budget limits (frame p95 5.182 ms on
  Windows 10/DesktopGL, NVIDIA GeForce GTX 1660 SUPER, 1280x720). The report,
  captures and metadata are attached to the GitHub Release.
- [ ] Publish and smoke a generated game on a clean target machine, including
  native dependencies, audio and input. An SDK-machine build is insufficient.

## Package identity and provenance

- [x] Build all five `.nupkg` files from one committed revision. Retain the
  archive audit output, `SHA256SUMS.txt` and `RELEASE_PROVENANCE.json` for the
  exact upload files.
- [x] Confirm every package and the generated template reference `0.3.0`.
- [x] Attach all five packages, including template and CLI, to the GitHub
  Release. They are local-feed packages, not packages published on nuget.org.

NuGet publication is explicitly deferred. The five IDs are not yet on
nuget.org; confirm publisher ownership and signing policy before the future
upload. Current local packages are unsigned (`dotnet nuget verify` reports
`NU3004`). The SHA-256 manifest detects byte changes but does not authenticate
an author. [NuGet.org repository-signs packages](https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference)
after publication; [author signing requires a suitable registered certificate](https://learn.microsoft.com/en-us/nuget/create-packages/sign-a-package).

For local use from this source checkout, run `eng/install-template.ps1` and
`eng/install-cli.ps1`. For GitHub Release downloads, keep all five `.nupkg`
files together in one local package source before installing the template or
CLI; generated games must restore the matching core and optional packages.

## Tag and GitHub Release

- [x] Record the exact commit SHA and verify a clean source tree.
- [x] Create annotated `v0.3.0` on that commit only after candidate validation.
- [x] Create the GitHub Release from that tag with migration notes, known limits,
  the five exact `.nupkg` archives and their SHA-256 manifest.
- [x] Verify archive hashes after downloading the release assets and confirm
  the tag points to the package-building commit.

Never publish or move a tag to make incomplete evidence appear complete.
