# ToolSwap

Tool parking, attachment, detachment and authenticated coordination with AutoArm.

## Files

| File | Purpose |
| --- | --- |
| `AutoArm.ParkPoses.cs.txt` | Stored tool park poses and preference restoration. |
| `AutoArm.ToolDiscovery.cs.txt` | Tool markers, mounts and merge-pair discovery. |
| `AutoArm.ToolFacade.cs.txt` | ToolSwap-specific facade used by the shared host. |
| `AutoArm.ToolGeometry.cs.txt` | Tool-region, coupler and stand geometry checks. |
| `AutoArm.ToolHost.cs.txt` | ToolSwap core setup and lifecycle. |
| `AutoArm.ToolPrefix.cs.txt` | Standalone ToolSwap entry point, state and commands. |
| `AutoArm.Tools.cs.txt` | Tool operations, docking, merge capture and transition state machine. |
| `AutoArm.TopInfo.cs.txt` | Rotor-top inspection and connector compatibility. |

## Working rules

- Edit these fragments, not generated `_Source.txt` or `_Compact.txt` files.
- Treat this as the approved 1.1 snapshot. Prefer making changes in experimental before explicit promotion.
- Shared fragments are under `../shared`; preserve their callers and wire/configuration contracts.
- Build from the repo root with `./tools/Build.ps1 -Track stable -Scripts ToolSwap`.
- Preserve C#6 and the 100,000 UTF-16 compact-script limit. Validate against installed SE rewriting; simulation does not prove live physics.
