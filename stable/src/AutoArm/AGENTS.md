# AutoArm

Main arm controller: discovery, cockpit input, pose control and service ownership.

## Files

| File | Purpose |
| --- | --- |
| `AutoArm.ArmFacade.cs.txt` | Public host facade for arm-specific operations. |
| `AutoArm.ArmTip.cs.txt` | Moving-tip discovery and bare/mounted tool endpoint handling. |
| `AutoArm.Config.cs.txt` | Configuration parsing, inheritance, validation and effective settings. |
| `AutoArm.Control.cs.txt` | Arm movement, observation, hold, feedback and constrained rate solving. |
| `AutoArm.Path.cs.txt` | Waypoint progression, pose tolerances and Home motion. |
| `AutoArm.Pivot.cs.txt` | Optional empirical pivot diagnostic. |
| `AutoArm.Prefix.cs.txt` | Standalone arm entry point, state and public command dispatch. |
| `AutoArm.Safety.cs.txt` | Arm-side Safety service reports, heartbeat refusal and collision-rate guidance. |
| `AutoArm.Services.cs.txt` | Authenticated external service sessions and motion ownership. |
| `AutoArm.ToolClient.cs.txt` | Arm-side authenticated ToolSwap session and transition handling. |
| `AutoArm.ToolControl.cs.txt` | Arm-side tool motion and docking policy. |
| `AutoArm.Topology.cs.txt` | Actuator graph discovery, parallel stages, live verification and synchronized writes. |

## Working rules

- Edit these fragments, not generated `_Source.txt` or `_Compact.txt` files.
- Treat this as the approved 1.1 snapshot. Prefer making changes in experimental before explicit promotion.
- Shared fragments are under `../shared`; preserve their callers and wire/configuration contracts.
- Build from the repo root with `./Build.ps1 -Track stable -Scripts AutoArm`.
- Preserve C#6 and the 100,000 UTF-16 compact-script limit. Validate against installed SE rewriting; simulation does not prove live physics.
