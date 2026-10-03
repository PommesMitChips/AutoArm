# PassingArm

WIP arm-side passing-lane and constraint-repair integration.

## Files

| File | Purpose |
| --- | --- |
| `AutoArm.NavigationArm.cs.txt` | Experimental arm-side passing-lane hooks. |

## Working rules

- Edit these fragments, not generated `_Source.txt` or `_Compact.txt` files.
- This is the WIP track. Prototypes remain experimental until explicitly confirmed and promoted.
- Shared fragments are under `../shared`; preserve their callers and wire/configuration contracts.
- Build from the repo root with `./Build.ps1 -Track experimental -Scripts PassingArm`.
- Preserve C#6 and the 100,000 UTF-16 compact-script limit. Validate against installed SE rewriting; simulation does not prove live physics.

## Existing WIP snapshot

`PassingArm.Program.cs.txt` is the complete editable WIP snapshot preserved from
the pre-cleanup working tree, with release labels normalized to 1.1. Normal
builds assemble/compact this file. The partial algorithm files are retained for
`-RefreshPrototypes`, which explicitly applies the guarded recipes to the current
core; that refresh can reveal unresolved size/compatibility limits.
