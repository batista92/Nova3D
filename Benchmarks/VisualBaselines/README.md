# Visual baselines

Committed visual references are grouped by scene and capture-contract version:

```text
<scene>/v<version>/
  baseline.png
  baseline.capture.json
  baseline.json
```

Do not replace these files by copying over them manually. Capture the controlled
scene, review it, then use `nova3d visual update-baseline ... --accept` so hashes,
environment and tolerances remain synchronized. See `Docs/performance.md`.

The initial suite contains `city-benchmark`, `pbr-material-csm` and
`gltf-static-animated`. Run `eng/run-visual-regression.ps1` on the controlled
graphics machine to capture and compare all three.
