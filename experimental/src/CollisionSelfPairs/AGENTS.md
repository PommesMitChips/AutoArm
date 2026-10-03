# CollisionSelfPairs

WIP self-collision-pair exclusion experiment.

## Files

| File | Purpose |
| --- | --- |
| `AutoArm.SelfPairs.cs.txt` | Experimental safe self-pair exclusion and reporting. |

## Working rules

- Edit these fragments, not generated `_Source.txt` or `_Compact.txt` files.
- This is the WIP track. Prototypes remain experimental until explicitly confirmed and promoted.
- Shared fragments are under `../shared`; preserve their callers and wire/configuration contracts.
- Build from the repo root with `./Build.ps1 -Track experimental -Scripts CollisionSelfPairs`.
- Preserve C#6 and the 100,000 UTF-16 compact-script limit. Validate against installed SE rewriting; simulation does not prove live physics.
