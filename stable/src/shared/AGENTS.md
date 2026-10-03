# shared

Reusable host, mathematics, link transport and saved-drive fragments; not a standalone script.

## Files

| File | Purpose |
| --- | --- |
| `AutoArm.CoreFacade.cs.txt` | Shared ArmCore adapter for host lifecycle and per-instance state. |
| `AutoArm.Host.cs.txt` | Multi-arm PB host template and per-arm routing; release label is injected at build time. |
| `AutoArm.Link.cs.txt` | Authenticated protocol transport and frame/session utilities. |
| `AutoArm.Math.cs.txt` | Shared vectors, rotations, transforms and numerical helpers. |
| `AutoArm.SavedDrives.cs.txt` | Saved actuator preferences and bounded cleanup. |

## Working rules

- Edit these fragments, not generated `_Source.txt` or `_Compact.txt` files.
- Treat this as the approved 1.1 snapshot. Prefer making changes in experimental before explicit promotion.
- Shared fragments are under `../shared`; preserve their callers and wire/configuration contracts.
- Build from the repo root with `./Build.ps1 -Track stable -Scripts AutoArm,ToolSwap`.
- Preserve C#6 and the 100,000 UTF-16 compact-script limit. Validate against installed SE rewriting; simulation does not prove live physics.
