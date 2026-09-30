# AutoArm

AutoArm is a mechanical-arm control program for the Space Engineers programmable block. It simplifies setup of robotic arms to configuring the start and endpoint of an arm, while allowing later fine tuning of parameters via CustomData within the programmable block.

## Install and choose a setup

Stop the previous controller before replacing its script. Set the one required code value, `const string ArmName = "Arm 1";`, then follow one of the three setups below. Names shown are actual block names; do not type a wildcard `*` into them. Configuration is the **programmable block's Custom Data**, not the individual parts' Custom Data.

Run `Version` first. **Parking and tool swapping require AutoArm 2.5.** Until its PR is merged, GitHub's `main` contains v2.4. Use [the v2.5 branch's complete compact script](https://github.com/PommesMitChips/AutoArm/blob/release/v2.5-tool-head-swap/AutoArm_Compact.txt) for those features.

**v2.5 requires Custom Data Format 3.** If existing data has a different or missing format, record the preferences you want to retain, delete Custom Data, and run `Reload`. The loader rejects older formats instead of migrating them. A populated actuator table is tied to this PB's saved identity cache; clear `Actuators` when installing into another PB.

Longer `ArmName` values consume the small remaining character allowance. Speed and acceleration caps are not measurements of physical momentum. Motor torque/force, travel limits and game physics still determine what the arm can achieve.

### 1. Simple arm: one fixed tool, no automatic parking

| Part | Example block name | Requirement |
| --- | --- | --- |
| First attached piston, rotor or hinge on the ship | `Arm 1 - Base - Rotor` | Exactly one mechanical block name starts with `Arm 1 - Base` |
| Head reference, such as one drill or camera | `Arm 1 - Head - Drill` | Exactly one terminal block name starts with `Arm 1 - Head` |
| Other joints | `Reach Piston`, `Elbow Hinge`, etc. | No naming scheme required |
| Other drills, merges, cameras or spare tools | `Arm 1 - Drill 2`, `Arm 1 - ToolMerge 1`, etc. | Do not start their names with `Arm 1 - Head` |
| Operating cockpit | Any name, e.g. `Arm Controller` | Use the active cockpit; its name is not a selector |

The head marker identifies one position and orientation, not every block on the head. With tools disabled, `Arm 1 - Head 1 - Drill`, `Arm 1 - Head 1 - HeadMerge` and `Arm 1 - Head 2 - Welder` all match the same broad head prefix. Naming every tool part that way makes discovery ambiguous. Similarly, do not name accessory mechanisms `Arm 1 - Base ...`.

For a fresh installation use [examples/SimpleArm.ini](examples/SimpleArm.ini). The essential setting is:

```ini
[Tools]
Enabled=false
```

Keep `Heads` and `Mount` empty in this mode. Run `Reload`, wait for stopped setup to finish, then `Check` and `On`. Additional physical drills can belong to the same tool; only one is the marked reference.

### 2. Park setup: one detachable tool and a parking port

Automatic `Park` **merges the tool to support and detaches it from the arm**. It is not a command to fold an attached arm to a stored pose; use `SetHome`/`GoHome` for that.

Build this arrangement, using an end coupler separate from the marked base:

```text
Ship -> Arm 1 - Base - Rotor -> arm joints -> Arm 1 - ToolMount (rotor/hinge BASE)
                                                    |
                                           detachable rotor/hinge TOP
                                                    |
                                      tool: drill + head-side merge

Parking support: ship/stand -> Arm 1 - ParkMerge 1 >< tool head-side merge
```

| Part | Exact example name |
| --- | --- |
| First arm joint | `Arm 1 - Base - Rotor` |
| Detachable end rotor/hinge **base** | `Arm 1 - ToolMount` |
| Drill used as the marker | `Arm 1 - Head 1 - Drill` |
| Merge carried by the tool | `Arm 1 - Head 1 - HeadMerge` |
| Merge fixed to the parking support | `Arm 1 - ParkMerge 1` |
| Detachable rotor/hinge **top**, carried by the tool | No special name required; discovered from the tool's occupied cells |

The marker, selected head-side merges and coupler top must belong to the same connected tool region. `ToolMount` is the actuated base remaining on the arm, not the detachable top. It must not also be the `Arm 1 - Base` marker: the script needs an upstream moving arm when the mount is bare. Use compatible supported vanilla rotor/hinge parts.

Use [examples/Park.ini](examples/Park.ini), or replace the corresponding sections of existing PB Custom Data with:

```ini
[Tools]
Enabled=true
Mount=Arm 1 - ToolMount
Heads=
|Arm 1 - Head 1 - Drill
ScanHeadMerges=false
ScanStandMerges=false
ScanParkMerges=false
ParkMerges=
|Arm 1 - ParkMerge 1

[Tool01]
Name=Head 1
HeadMerges=
|Arm 1 - Head 1 - HeadMerge
StandMerges=
```

Run `Reload`, wait until the scan finishes OFF, then inspect `ToolInfo`. For initial manual movement, the tool is attached to its mount and its head merge is not connected to the stand. `Check` and `On` enable manual operation. `Park` or `Park 1` performs automatic parking and leaves a bare mount OFF. `Tool Head 1` picks the same supported tool up again; it also finishes OFF. Run `On` to resume manual use.

If the head uses multiple support merges, enumerate and list every required head merge and matching park merge. They must form a compatible pattern, and all selected head ports must be supported before detachment. `Park 2` selects the **primary merge named `Arm 1 - ParkMerge 2`**, not a tool index or an arbitrary named bay. Without a configured generic park port, use tool selection for a swap to a per-tool stand; naming something `StandMerge` does not by itself make it a `Park` destination.

### 3. Tool swap setup: drill and welder with their own stands

**The six names in the reported screenshot are valid with this explicit configuration.** Keep these names and add the marked base and end coupler:

| Part | Exact block name | Location |
| --- | --- | --- |
| First arm joint | `Arm 1 - Base - Rotor` | Ship-to-arm joint |
| Detachable end rotor/hinge base | `Arm 1 - ToolMount` | Remains on the arm |
| Tool 1 marker | `Arm 1 - Head 1 - Drill` | On drill tool |
| Tool 1 merge | `Arm 1 - Head 1 - HeadMerge` | On drill tool |
| Tool 1 stand merge | `Arm 1 - Head 1 - StandMerge` | On fixed drill stand |
| Tool 2 marker | `Arm 1 - Head 2 - Welder` | On welder tool |
| Tool 2 merge | `Arm 1 - Head 2 - HeadMerge` | On welder tool |
| Tool 2 stand merge | `Arm 1 - Head 2 - StandMerge` | On fixed welder stand |

Each tool carries its compatible detachable rotor/hinge top. A parked tool's top is detached, and its own merge is locked face-to-face with its stand merge. For the first scan, either have the drill mounted and free of merge support while the welder is parked, or have both tools parked and the arm mount bare. All configured markers/merges must be visible to the PB. A marker name in `Heads` is the **complete terminal block name**, not an alias or a prefix.

Use [examples/ToolSwap.ini](examples/ToolSwap.ini). The naming-related sections are:

```ini
[Tools]
Enabled=true
Mount=Arm 1 - ToolMount
Heads=
|Arm 1 - Head 1 - Drill
|Arm 1 - Head 2 - Welder
ScanHeadMerges=false
ScanStandMerges=false
ScanParkMerges=false
ParkMerges=

[Tool01]
Name=Head 1
HeadMerges=
|Arm 1 - Head 1 - HeadMerge
StandMerges=
|Arm 1 - Head 1 - StandMerge

[Tool02]
Name=Head 2
HeadMerges=
|Arm 1 - Head 2 - HeadMerge
StandMerges=
|Arm 1 - Head 2 - StandMerge
```

`Name=Head 1` is only a convenient command alias. It does not rename the marker or change automatic stand prefixes. The explicit merge lists above accept your unnumbered `StandMerge` names and avoid prefix inference altogether. `Scan...=false` disables automatic adoption; explicit names still work, and physical facing-merge inspection still identifies the tool region.

Run `Reload`, let scanning finish OFF, then `ToolInfo`. It should show both aliases, their mounted/detached state, and `merges 1/1` for each one-port tool/stand profile. It does not print marker/top identities; those are checked internally. Use `Tool Head 2` (or `Tool 2`) to park the drill on its own stand and pick up the welder. `Tool Head 1` reverses the swap. Successful changes finish OFF; run `On` for manual control. Generic `Park` is optional: add `Arm 1 - ParkMerge 1` to `[Tools] ParkMerges` if you want a separate common parking support.

If you prefer automatic merge adoption, set the corresponding scans to `true`. Head-side merges are adopted from the physically scanned tool region, irrespective of their names. Stand auto-adoption uses `StandPrefix`; when it is blank, it is derived from the **full marker name**. With marker `Arm 1 - Head 1 - Drill`, the default is `Arm 1 - Head 1 - Drill - StandMerge `, not `Arm 1 - Head 1 - StandMerge `. The simpler automatic naming scheme in the preserved release example assumes the marker is named exactly `Arm 1 - Head 1`. Number automatic stand and park merges, starting with `1`.

### Fixing “Head*; found 6”

This message means discovery is using the simple-arm head-prefix rule and sees six matches. `Arm 1 - Head 1 - HeadMerge` and `Arm 1 - Head 1 - StandMerge` count just as much as the drill. It does not mean the arm contains six selected tool markers.

- **For a simple arm:** keep only one reference block beginning `Arm 1 - Head`; rename the other five so they do not use that prefix.
- **For this two-tool setup:** run `Version` and use v2.5, set `[Tools] Enabled=true`, specify the end-coupler base in `Mount`, and use the exact two marker names and merge lists above. Edit existing sections rather than adding duplicate `[Tools]` sections. Run `Reload` after editing; `Check` alone does not load tool configuration.
- **For missing-name errors:** match spelling, spaces and capitalization exactly. `Arm 1 - Head 1` is not the full name `Arm 1 - Head 1 - Drill`. Do not leave a profile for an absent tool; remove its `Heads` entry and keep `ToolNN` sections in list order.

For a fresh setup, the linked INI examples include `[AutoArm] Arm=Arm 1` and `Format=3` and intentionally leave `Actuators` blank. When editing an existing valid setup, retain your current `[Config]` settings and actuator table rather than replacing them unnecessarily.

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

Tools are optional and disabled by default. The three setup recipes above distinguish simple prefix discovery from configured exact tool markers. The swap example uses explicit merge names:

- Enable `[Tools]`, set `Mount` to the exact unique name of the detachable rotor/hinge **base**, and list exact head-marker names in `Heads`.
- Each `ToolNN` section follows that list's order. `Name` provides a convenient command alias such as `Head 1`.
- With a marker named exactly `Arm 1 - Head 1`, default stand merges use `Arm 1 - Head 1 - StandMerge 1`, etc. Longer marker names change the inferred prefix; explicit lists avoid this. Generic park ports use `Arm 1 - ParkMerge 1`, etc.
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

## Acknowledgements

Development began with a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS). Since then, AutoArm has developed its own automatic discovery and task-space control architecture.
