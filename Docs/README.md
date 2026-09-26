# Nova3D documentation

Nova3D is a code-first 3D toolkit over MonoGame. Start with the document that
matches the work being done instead of reading the repository chronologically.

## First project

1. [AI quickstart](AI_QUICKSTART.md) — compact contract and context router.
2. [Agent skill](agent-skill.md) — progressive context and diagnostic workflow.
3. [Getting started](getting-started.md) — install the template and create a game.
4. [Game project guide](game-project-guide.md) — lifecycle, folder ownership,
   optional modules, audio and executable builds.
5. [Recipes](Recipes/README.md) — short, task-oriented implementation paths.
6. [Public API index](API_INDEX.md) — purpose, package and ownership by type.
7. [Architecture](architecture.md) — module boundaries and dependency direction.
8. [AI guide](AI_GUIDE.md) — expanded constraints for architectural changes.

## Work by topic

| Task | Read first |
|---|---|
| Rendering, meshes, frame passes | [rendering.md](rendering.md) |
| Materials and color space | [materials.md](materials.md) |
| Shader edits and sampler limits | [shaders.md](shaders.md) |
| Terrain, editing and streaming | [terrain.md](terrain.md) |
| GLB/glTF import | [GltfImport.md](GltfImport.md) |
| Runtime assets and hot reload | [AssetManagement.md](AssetManagement.md) |
| BEPU physics | [physics.md](physics.md) |
| Gum menus and HUD | [ui.md](ui.md) |
| Profiling and regressions | [performance.md](performance.md) |
| Packaging and release | [releasing.md](releasing.md) |
| Run repository or game validation | [validation.md](validation.md) |
| A failure in a generated game | [troubleshooting.md](troubleshooting.md) |
| Implement a common game feature | [Recipes/README.md](Recipes/README.md) |
| Find the supported public type | [API_INDEX.md](API_INDEX.md) |

## Evaluating Nova3D in a real game

Keep game-specific code in the game repository. Record toolkit problems using
[NOVA3D_EVALUATION_TEMPLATE.md](NOVA3D_EVALUATION_TEMPLATE.md). A report must
contain a minimal reproduction and evidence before engine code is changed.

Classify every issue as one of:

- `Game`: gameplay/content code owns the fix;
- `Documentation`: the supported path works but is unclear or missing;
- `Nova3D.Core`: reusable rendering/world/asset behavior is wrong;
- `Nova3D.Physics.Bepu`: the optional physics integration owns the behavior;
- `Nova3D.UI.Gum`: the optional UI integration owns the behavior;
- `Nova3D.Templates`: generated project structure or conditional content;
- `Upstream`: MonoGame, Gum, BEPU, driver or platform issue.

Do not copy Nova3D source into a game as a workaround. Reproduce, fix the owning
package, add a regression check, rebuild the package and retest the consumer.

The implementation plan for reducing agent context and effort is tracked in
`FASE 5 — AI DEVELOPER EXPERIENCE` in [ROADMAP.md](../ROADMAP.md). Marble3D is
the external consumer and final gate for that phase.
