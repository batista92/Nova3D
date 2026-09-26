# Agent skill

Nova3D versions the `nova3d-game-development` skill at:

```text
skills/nova3d-game-development/SKILL.md
```

The project template installs that file at
`.agents/skills/nova3d-game-development/SKILL.md` in every generated game.
Compatible agents can discover it automatically there. It may also be invoked
explicitly as `$nova3d-game-development` when named skills are supported.

The skill is a context router, not a second manual. It always begins with
`Docs/AI_QUICKSTART.md`, then loads only the recipe, API catalog or topic needed
for the current task. It also defines the game/toolkit ownership decision,
diagnostic protocol and validation command.

## Expected agent flow

```text
request
  -> AI_QUICKSTART
  -> one recipe or topic document
  -> existing API/sample search
  -> scoped implementation
  -> eng/validate.ps1 or Release game build
  -> explicit verified/not-verified report
```

Do not copy the full documentation into the skill. When a rule changes, update
its authoritative topic document; update the skill only if routing or the
cross-cutting workflow changed.

## Validation

The normal repository command verifies that generated template projects contain
the skill:

```powershell
.\eng\validate.ps1
```

Skill metadata and structure can be checked with the Codex `skill-creator`
validator when that system skill is available. Behavioral validation remains
the AI Gate in A7: a new agent must build the external Marble3D project without
the CityBuilder conversation history.
