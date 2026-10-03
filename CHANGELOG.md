# Changelog

All notable changes to Nova3D are recorded in this file.

## [Unreleased]

### Added

- versioned `nova3d.scene` version 1 authoring contract, strict CPU-only parser,
  deterministic writer, aggregated semantic validator, explicit component
  descriptor registry, JSON Schema, authoring recipe and minimal reference
  document for the v0.3 scene pipeline;
- two-phase scene preparation and instantiation, parent-first resolved
  transforms, graphics-thread affinity, disposable runtime ownership and
  reverse-order rollback for partial failures;
- explicit built-in scene components for renderable GLB/glTF models, cameras,
  directional lights, spawn points and tags, including stable property
  diagnostics and optional-module registry extension;
- contained scene asset resolution with actionable path diagnostics,
  thread-affine reference-counted caching, explicit model leases and
  cancellation-safe scene rollback;
- CPU-only `scene/1` prefab expansion with isolated IDs, nested reuse, typed
  component/transform overrides and recursive-reference diagnostics;
- node/model bounds and name-anchor diagnostics, a `DataDrivenScene` sample,
  and an optional `--scene` project-template variant;
- glTF skinning data import with four vertex influences, 48-joint local
  palettes, inverse bind matrices and LINEAR/STEP TRS animation clips;
- allocation-free skeleton pose evaluation, clip playback controls and simple
  two-clip cross-fading;
- PBR and shadow skinning techniques, conservative animated bounds, skeleton
  debug rendering and the external-GLB `AnimatedCharacter` sample;
- audio buses with mute/fades, bounded native SFX voice pools, 3D
  listener/emitter playback, music looping and focus-loss lifecycle;
- platform user-data paths and strict versioned JSON persistence with atomic
  replacement, backup recovery, semantic validation, save slots and migration;
- separate `Nova3D.Cli` .NET tool package, repository-local installer and
  isolated package installation regression;
- `nova3d doctor` diagnostics for SDK, project target, Nova3D reference,
  restore state and declared MonoGame content paths;
- distributed `nova3d validate` restore, doctor preflight and Release/MGCB
  build for a consuming project;
- CPU-only `nova3d inspect <model.glb>` container, external-resource and
  documented importer-compatibility diagnostics;
- `nova3d inspect <scene.json>` using the authoritative scene parser,
  built-in validation, prefab expansion and portable asset resolution;
- transactional `nova3d publish --runtime <RID>` self-contained Release
  publishing to the unambiguous `dist/<RID>` directory;
- versioned Nova3D CLI automation contract with compact human output, stable
  exit codes, single-document `--format json`, closed stdin and actionable
  failures;
- deterministic one-shot visual capture with fixed plan inputs, warm-up frame,
  PNG output and versioned GPU/backend metadata, integrated into CityBenchmark;
- visual regression CLI with scene/version baselines, explicit reviewed updates,
  configurable pixel tolerances, comparison metrics and PNG diff evidence;
- initial controlled-GPU visual suite covering city terrain/vegetation,
  CSM, water/PostFX, focused PBR materials and generated static/animated GLBs;
- versioned CityBenchmark performance reports and committed budgets for frame,
  CPU passes, draws, triangles and candidates, with a machine-readable CLI gate
  and a controlled script that preserves the physics regression;

### Changed

- coordinated package and generated-template references for the 0.3.0 release
  candidate; added Windows package/template CI, Linux CPU-contract CI and an
  explicit compatibility/format-version guide;
- audio coordination and persistence remain in the core `Nova3D` package after
  a two-consumer repetition/dependency audit; BEPU and Gum remain optional;
- Windows publishing now uses an explicit `dist/win-x64` output so RID staging
  under `bin/` cannot be mistaken for the distributable folder;
- generated projects ignore `dist/`.

## [0.2.0] - 2026-09-26

### Added

- optional `Nova3D.Physics.Bepu` integration with fixed-step simulation;
- optional `Nova3D.UI.Gum` integration for menus, HUD and navigation;
- `--physics` and `--ui` project-template variants;
- focused recipes, API index, troubleshooting and agent skill;
- repository, sample and generated-project validation;
- automated package payload, metadata and dependency auditing;
- external Marble3D AI gate.

### Changed

- the template, optional modules and core package now share version `0.2.0`;
- the local template installer discovers the package produced by the build.

### Fixed

- generated projects now ignore `bin/` and `obj/`;
- issue ownership includes `Nova3D.Templates`.

## [0.1.0] - 2026-09-20

- initial reusable Nova3D toolkit extracted from the CityBuilder benchmarks;
- rendering, terrain, vegetation, streaming, water, post-processing and glTF;
- MIT licensing and initial package metadata.
