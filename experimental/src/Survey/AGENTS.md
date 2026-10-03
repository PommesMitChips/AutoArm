# Survey

Read-only ArmService geometry and starting-pose survey.

## Files

| File | Purpose |
| --- | --- |
| `AutoArm.Survey.cs.txt` | Editable read-only geometry-report script. |

## Working rules

- Edit these fragments, not generated `_Source.txt` or `_Compact.txt` files.
- This is the WIP track. Prototypes remain experimental until explicitly confirmed and promoted.
- Shared fragments are under `../shared`; preserve their callers and wire/configuration contracts.
- Build from the repo root with `./tools/Build.ps1 -Track experimental -Scripts Survey`.
- Preserve C#6 and the 100,000 UTF-16 compact-script limit. Validate against installed SE rewriting; simulation does not prove live physics.
