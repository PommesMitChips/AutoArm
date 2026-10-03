# Collision

Optional conservative construct/arm collision Safety service.

## Files

| File | Purpose |
| --- | --- |
| `AutoArm.Collision.cs.txt` | Standalone collision-service implementation and settings. |

## Working rules

- Edit these fragments, not generated `_Source.txt` or `_Compact.txt` files.
- Treat this as the approved 1.1 snapshot. Prefer making changes in experimental before explicit promotion.
- Shared fragments are under `../shared`; preserve their callers and wire/configuration contracts.
- Build from the repo root with `./tools/Build.ps1 -Track stable -Scripts Collision`.
- Preserve C#6 and the 100,000 UTF-16 compact-script limit. Validate against installed SE rewriting; simulation does not prove live physics.
