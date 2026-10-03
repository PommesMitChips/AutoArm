# Planner

WIP standalone whole-arm planning service and modeled planning world.

## Files

| File | Purpose |
| --- | --- |
| `AutoArm.Planner.cs.txt` | Experimental planning PB entry point, search and ArmService ownership. |
| `AutoArm.PlanningWorld.cs.txt` | Experimental modeled collision world used by the planner. |

## Working rules

- Edit these fragments, not generated `_Source.txt` or `_Compact.txt` files.
- This is the WIP track. Prototypes remain experimental until explicitly confirmed and promoted.
- Shared fragments are under `../shared`; preserve their callers and wire/configuration contracts.
- Build from the repo root with `./tools/Build.ps1 -Track experimental -Scripts Planner`.
- Preserve C#6 and the 100,000 UTF-16 compact-script limit. Validate against installed SE rewriting; simulation does not prove live physics.

## Existing WIP snapshot

`Planner.Program.cs.txt` is the complete editable WIP snapshot preserved from
the pre-cleanup working tree, with release labels normalized to 1.1. Normal
builds assemble/compact this file. The partial algorithm files are retained for
`-RefreshPrototypes`, which explicitly applies the guarded recipes to the current
core; that refresh can reveal unresolved size/compatibility limits.
