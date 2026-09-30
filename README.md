# AutoArm

Automatic mechanical-arm control for the **Space Engineers programmable block**. Name the base and head, and AutoArm discovers the joints and generates editable configuration.

This branch contains **v2.5 Tool Head Swap**, proposed against the imported v2.4 `main`. The playable script is [AutoArm_Compact.txt](AutoArm_Compact.txt): **99,883 characters**, leaving 117 under the game's 100,000-character limit. Paste the entire file into the PB editor. You do not need the local development tools to use it.

## Install

1. Stop the previous controller before replacing its script.
2. Set the one required code value: `const string ArmName = "Arm 1";`.
3. Name the base actuator `Arm 1 - Base*` and the head reference block `Arm 1 - Head*`, for example `Arm 1 - Base - Rotor` and `Arm 1 - Head - Drill`.
4. AutoArm starts OFF and generates Custom Data. Edit settings and actuator weights, run `Reload`, then `Check` and `On` after stopped setup is ready.

**v2.5 requires Custom Data Format 3.** If existing data has a different or missing format, record the preferences you want to retain, delete Custom Data, and run `Reload`. The loader rejects older formats instead of migrating them. A populated actuator table is tied to this PB's saved identity cache; clear `Actuators` when installing into another PB.

Longer `ArmName` values consume the small remaining character allowance. Speed and acceleration caps are not measurements of physical momentum. Motor torque/force, travel limits and game physics still determine what the arm can achieve.

## Hardware and control

Pistons, rotors and hinges can appear throughout a serial chain, including at the base and with spatial offsets or different orientations. Supported parallel branches have equal stage counts and matching synchronized motion. Separated piston rails are supported; rotary counterparts must be coaxial. Opposite-facing compatible rotors/hinges receive signed native velocities to move as one. General closed linkages, unequal branch lengths and variable-ratio mechanisms are refused.

Translation keeps the head's orientation target. Live pilot input remains additive to correction, with opposing correction limited to half the pilot demand before physical allocation. Bounded learned bias counters sustained drift, including gravity in the simulated tests.

| Mode | W/S | A/D | C/Space |
| --- | --- | --- | --- |
| `HEAD` | Head Forward/Back | Head Left/Right | Head Down/Up |
| `HRZ` | Active cockpit Forward/Back | Cockpit Left/Right | Cockpit Down/Up |
| `VRT` | Cockpit screen Up/Down | Cockpit Left/Right | Into/out of the view |

Q/E rolls around the actual head. Optional mouse pitch/yaw follows the current view. Set `[Config] MovementMode=HEAD|HRZ|VRT` for the default, or use `Mode HEAD`, `Mode HRZ`, `Mode VRT` temporarily. `ReadMouse On|Off|Toggle` controls mouse input. The keys shown assume the game's usual bindings.

Useful toolbar Run arguments include `On`, `Stop`, `Hold`, `Check`, `Reload`, `Info`, `Joints`, `SetHome`, `GoHome`, `PitchUp`, `PitchDown`, `YawLeft`, `YawRight`, `RollLeft`, `RollRight` and `Face <direction>`. Custom Data contains the full command list and setting ranges. Runtime speed/mode commands are temporary; Reload restores configured defaults.

## Tool Head Swap

Tools are optional and disabled by default. Start with [examples/ToolSwap.ini](examples/ToolSwap.ini):

- Enable `[Tools]`, set `Mount` to the exact unique name of the detachable rotor/hinge **base**, and list exact head-marker names in `Heads`.
- Each `ToolNN` section follows that list's order. `Name` provides a convenient command alias such as `Head 1`.
- Default stand merges use `Arm 1 - Head 1 - StandMerge 1`, etc. Generic park ports use `Arm 1 - ParkMerge 1`, etc.
- Explicit `HeadMerges`, `StandMerges` and `ParkMerges` lists work with automatic adoption disabled. `ScanHeadMerges`, `ScanStandMerges`, `ScanParkMerges` control adoption.

The parked-tool scan walks occupied cells from the marker and stops only across adjacent opposed merge faces: head merge **><** stand merge. Parallel/non-facing merges are not barriers. This identifies the tool's exact rotor/hinge top even when it shares the stand grid. Supported vanilla couplers do not require a teaching mount.

`Tool Head 2` or `Tool 2` parks the current tool on its stand, verifies all selected support ports, detaches the configured mount, approaches and attaches the selected incoming top, then disables only that tool's head-side merges. It verifies exact reciprocal attachment, coupling pose and actual grid separation before rebuilding. `Park` uses generic park ports; `Park 2` selects a primary enumerated port. `ToolInfo` reports state.

Completion stays **OFF**; run `On` to resume manual control. Pilot input, `SwapCancel`, `Stop` or `Off` cancels automatic motion and retains the existing mechanical/support state. After cancellation or interruption, explicitly run `ToolScan` to reconcile the stopped setup. Passive startup scanning does not clear recovery.

Automatic operations require proven compatible support. Unknown/modded or flipped couplers and incompatible attachment geometry are refused. **There is no collision checking or path planning**; approach clearance does not guarantee a clear route. Volumetric restrictions remain future work.

See the [v2.5 release notes](releases/v2.5/AutoArm_v2_5_Notes.txt) for complete setup, restrictions and recovery details. Historical filenames in those notes refer to the exact preserved artifacts alongside them. [examples/CustomData.ini](examples/CustomData.ini) shows the generated ten-joint configuration; generate your own identity table rather than copying its populated actuator rows.

## Build and test

Development requires Windows, the .NET 10 SDK, the .NET Framework reference assemblies, and a local Space Engineers installation. Game assemblies and model assets are not included.

From this repository:

```powershell
.\tools\Build-AutoArm.ps1 -Test
```

For a nondefault game location:

```powershell
.\tools\Build-AutoArm.ps1 -GameBin 'D:\SteamLibrary\steamapps\common\SpaceEngineers\Bin64' -Test
```

Edit the fragments in `src/`. The build regenerates `AutoArm_Source.txt` and `AutoArm_Compact.txt`, checks C# 6 against installed SE APIs, verifies token round-trip and identical method IL, and runs the simulated PB harness. Tests also regenerate `examples/CustomData.ini`. Preserved files under `releases/` are not rewritten by the build. [`src/AutoArm.Seam.md`](src/AutoArm.Seam.md) describes the implementation boundaries.

The relocated v2.5 suite passes **174,262 assertions**. Coverage includes topology discovery, signed parallel groups, additive/manual-priority input, gravity bias, movement frames, facing-merge boundaries, swap/park sequences, partial support, wrong tops, pose gates, cancellation/restart, startup drive stopping and Home attachment faults. Live SE physics, actual instruction costs and loaded merge timing remain unverified.

## History and acknowledgement

Saved snapshots from v2.0 through v2.5 are preserved in `releases/` and imported as ordered commits with annotated tags. Git timestamps record the reconstruction; undocumented intermediate commits or historical dates have not been invented. The earlier v2.1 branding snapshot is tagged `v2.1-preview` to distinguish it from the delivered AutoArm naming revision. The current full toolchain is introduced with v2.5; older tags preserve combined source rather than reconstructed fragments.

See [docs/HISTORY.md](docs/HISTORY.md) for the release timeline and [docs/ATTRIBUTION.md](docs/ATTRIBUTION.md) for acknowledgements. AutoArm grew from a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS); it is not an official MArmOS release or a drop-in fork.
