# Validation

Nova3D provides one command for deterministic build/package/template checks.
Run it from PowerShell at the repository root:

```powershell
.\eng\validate.ps1
```

The repository flow validates:

1. restore and Release build of the benchmark consumer;
2. MGCB shader/content compilation through that build;
3. the headless physics regression benchmark;
4. Release builds of all projects under `Samples/`;
5. Release packages for core, physics, UI and templates;
6. package identity, metadata, dependencies, required payload and forbidden
   `bin/`, `obj/` or unsafe paths;
7. isolated generation and Release builds of default, `--physics`, `--ui` and
   `--physics --ui` template variants.

The template smoke creates an isolated `NuGet.Config` containing only the local
package output and nuget.org. This prevents user-configured feeds and native
shell URL parsing from changing the result.

Successful command output is intentionally compact:

```text
RUN  repository Release build + MGCB
PASS repository Release build + MGCB (2.10s)
...
VALIDATION PASS | scope repository | steps 25 | 84.20s
```

On failure, the command output for the failed step is printed and the script
returns a nonzero exit code. Temporary template projects are preserved on
failure and their path is reported. Use `-KeepTemporary` to preserve them after
success as well.

## Validate a consuming game

Point the same command at its project:

```powershell
.\eng\validate.ps1 -GameProject C:\Games\Marble3D\Marble3D.csproj
```

This restores and builds the game in Release, including its MGCB content. It
does not launch a graphical window, judge visual output or replace manual input,
audio and clean-machine publish testing.

## Focused options

```powershell
.\eng\validate.ps1 -NoRestore
.\eng\validate.ps1 -SkipPhysicsBenchmark
.\eng\validate.ps1 -SkipTemplateSmoke
```

`-NoRestore` is intended for an already restored repository or game. Template
smoke projects still restore because they consume the packages created during
the same run.

Do not use skip switches as release evidence. They exist for fast local work;
the default command is the release baseline.
