# Nova3D CLI

Command-line validation and diagnostics for Nova3D projects.

Install from NuGet after publication:

```powershell
dotnet tool install --global Nova3D.Cli --version <VERSION>
nova3d --help
```

Diagnose a restored Nova3D project with:

```powershell
nova3d doctor C:\Games\MyGame\MyGame.csproj
nova3d validate C:\Games\MyGame\MyGame.csproj
nova3d inspect C:\Games\MyGame\Assets\Models\character.glb
nova3d inspect C:\Games\MyGame\Assets\Scenes\level01.scene.json
nova3d publish --runtime win-x64 C:\Games\MyGame\MyGame.csproj
nova3d visual compare C:\captures\city.png --baseline-root C:\VisualBaselines
nova3d performance check C:\reports\city.json --budget C:\budgets\city.json
```

Visual regression baselines are updated only with the explicit
`visual update-baseline ... --accept` command. Comparison produces numeric
metrics and a PNG diff and supports the global `--format json` automation mode.
Performance reports and budgets use versioned JSON contracts; hardware that
differs from the budget reference is reported as auxiliary evidence.

`publish` creates a self-contained Release build in the game's
`dist/<RID>` directory. Existing output is replaced only after the new publish
succeeds.

For CI and agents, append `--format json`. The CLI writes one versioned JSON
document to stdout, never prompts for input, and uses stable exit codes: `0`
success, `1` command failure, `2` invalid usage and `3` internal failure.

For a repository checkout, use `eng/install-cli.ps1` to pack the current source
and install it into an isolated tool path under `artifacts/tools`. Re-running
the script refreshes local source changes even before the package version is
bumped.
