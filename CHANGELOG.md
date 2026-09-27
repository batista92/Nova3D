# Changelog

All notable changes to Nova3D are recorded in this file.

## [Unreleased]

### Changed

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
