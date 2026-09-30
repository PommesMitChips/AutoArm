# AutoArm

AutoArm is a mechanical-arm control program for the Space Engineers programmable block. It simplifies setup of robotic arms to configuring the start and endpoint of an arm, while allowing later fine tuning of parameters via CustomData within the programmable block.

## Install AutoArm 2.5

1. Run `Stop` on the previous controller. Copy the **entire** [AutoArm_Compact.txt](AutoArm_Compact.txt) into the programmable block's script editor.
2. Set the code value `const string ArmName = "Arm 1";` to your arm's name, then check/compile the script in the PB editor. No local build tools are needed to play.
3. Run the PB with `Version` and confirm `AutoArm 2.5.1`. This patch fixes hidden-top discovery in the original 2.5 build. It starts OFF; a first discovery error is expected if the parts or tool profiles are not configured yet.
4. Choose a setup below, name the parts, and edit the **programmable block's Custom Data**. Do not put these settings in the drills, rotors or merge blocks' Custom Data.
5. Run `Reload`, let discovery/scanning finish OFF, then run `Check`. For parking or swapping, also inspect `ToolInfo`. Run `On` when setup is ready and the mounted tool is clear of its merge support.

The examples use `Arm 1`; substitute your chosen name in the block names, `[AutoArm] Arm`, `Mount` and profile lists. Names shown are actual block names. A wildcard `*` in an explanation means “begins with”; do not type it into a name.

| Setup | Starter Custom Data | Selection |
| --- | --- | --- |
| Fixed tool, ordinary arm control | [SimpleArm.ini](examples/SimpleArm.ini) | `[Tools] Enabled=false`; one head-prefix marker |
| One detachable tool and a common parking support | [Park.ini](examples/Park.ini) | Tools enabled; one exact marker, end mount and park ports |
| Several detachable tools with individual stands | [ToolSwap.ini](examples/ToolSwap.ini) | Tools enabled; exact marker and merge lists for each profile |

For a **fresh setup**, copy the chosen starter into PB Custom Data and adjust the names. Missing scalar settings are filled with defaults. For an **existing valid setup**, edit its existing sections to retain your speeds, tuning and actuator weights; do not append duplicate sections.

AutoArm 2.5 requires `[AutoArm] Format=3`. If older data has a different or missing format, save the preferences you want to re-enter, delete Custom Data, and run `Reload` to generate the current format. There is no format migration. A populated `Actuators` table belongs to this PB's saved identity cache; clear that value when installing into another PB so its own table is generated.

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
| Detachable rotor/hinge **top**, carried by the tool | No name required; learned while mounted, then located using saved geometry |

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

For first setup, have the tool attached to `ToolMount`. Run `Reload` or `ToolScan`, wait until the scan finishes OFF, then inspect `ToolInfo`. The scan learns the actual unnamed top's identity and coupling pose. For manual movement its head merge must not remain connected to the stand. `Check` and `On` enable manual operation. `Park` or `Park 1` performs automatic parking and leaves a bare mount OFF. `Tool Head 1` picks the learned supported tool up again; it also finishes OFF. Run `On` to resume manual use.

If the head uses multiple support merges, enumerate and list every required head merge and matching park merge. They must form a compatible pattern, and all selected head ports must be supported before detachment. `Park 2` selects the **primary merge named `Arm 1 - ParkMerge 2`**, not a tool index or an arbitrary named bay. Without a configured generic park port, use tool selection for a swap to a per-tool stand; naming something `StandMerge` does not by itself make it a `Park` destination.

### 3. Tool swap setup: drill and welder with their own stands

This example uses a drill tool and a welder tool. The merge blocks can share the `Head 1`/`Head 2` naming scheme because tool mode selects the exact configured markers:

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

Each tool carries its compatible detachable rotor/hinge top. A parked tool's top is detached, and its own merge is locked face-to-face with its stand merge. **Each new tool needs one setup mount on the configured `ToolMount` and a completed scan.** An already learned tool can be scanned while parked after restarting the PB. All configured markers/merges must be visible to the PB. A marker name in `Heads` is the **complete terminal block name**, not an alias or a prefix.

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

For initial learning:

1. With the drill attached to `ToolMount`, run `Reload` or `ToolScan` and let it finish OFF. `ToolInfo` should show `Head 1 mounted` and `Head 2 unlearned; mount once`. The unlearned welder does not prevent manual control of the drill; selecting it refuses before parking the drill.
2. Keep the script OFF. Support the drill with its merge blocks before manually detaching it. Attach the arm's `ToolMount` to the welder's existing unnamed top while the welder remains supported on its stand, then run `ToolScan` to learn it. A configured generic `Park` destination can be used to park the already learned drill; otherwise position/support it manually for this first setup.
3. Confirm the real mount is attached before disabling **only the currently mounted tool's head-side merges**. Wait for separation from its stand and run `ToolScan` again. Never remove support from a tool whose top is still detached.
4. Check `ToolInfo`: both profiles should now be learned, with mounted/detached state and `merges 1/1` for each one-port tool/stand profile. Use `Tool Head 1` or `Tool Head 2` for subsequent automatic swaps.

Coupler identity and marker-relative geometry are saved in this PB's Storage; keep that Storage when recompiling or replacing the script. Moving to a different PB, replacing a top/marker, or changing the coupling layout may require another setup mount. The tool's top remains unnamed throughout.

After learning, `Tool Head 2` (or `Tool 2`) parks the drill on its own stand and picks up the welder; `Tool Head 1` reverses it. Successful changes finish OFF; run `On` for manual control. Generic `Park` is optional: add `Arm 1 - ParkMerge 1` to `[Tools] ParkMerges` if you want a separate common parking support.

If you prefer automatic merge adoption, set the corresponding scans to `true`. Head-side merges are adopted from the physically scanned tool region, irrespective of their names. Stand auto-adoption uses `StandPrefix`; when it is blank, it is derived from the **full marker name**. With marker `Arm 1 - Head 1 - Drill`, the default is `Arm 1 - Head 1 - Drill - StandMerge `, not `Arm 1 - Head 1 - StandMerge `. For the simpler automatic naming convention, name the marker exactly `Arm 1 - Head 1` and its stand merges `Arm 1 - Head 1 - StandMerge 1`, etc. Number automatic stand and park merges, starting with `1`.

### Fixing “Head*; found 6”

This message means discovery is using the simple-arm head-prefix rule and sees six matches. `Arm 1 - Head 1 - HeadMerge` and `Arm 1 - Head 1 - StandMerge` count just as much as the drill. It does not mean the arm contains six selected tool markers.

- **For a simple arm:** keep only one reference block beginning `Arm 1 - Head`; rename all other matching parts so they do not use that prefix.
- **For a tool setup:** set `[Tools] Enabled=true`, specify the end-coupler base in `Mount`, and list the full marker names and merges as shown above. Run `Reload` after editing; `Check` alone does not load tool configuration. If `Version` does not report 2.5.1, install the current compact script first.
- **For missing-name errors:** match spelling, spaces and capitalization exactly. `Arm 1 - Head 1` is not the full name `Arm 1 - Head 1 - Drill`. Do not leave a profile for an absent tool; remove its `Heads` entry and keep `ToolNN` sections in list order.

For a fresh setup, the linked INI examples include `[AutoArm] Arm=Arm 1` and `Format=3` and intentionally leave `Actuators` blank. When editing an existing valid setup, retain your current `[Config]` settings and actuator table rather than replacing them unnecessarily.

### “No eligible top” and unlearned tools

An earlier v2.5 build tried to discover unnamed tops through `GetCubeBlock()`. The game's PB implementation hides non-terminal blocks from that call, including rotor/hinge tops. Having a real top directly attached to the welder was therefore insufficient for that scanner. This was a script bug, not a requirement to name the top or rebuild the tool.

The current build walks occupancy through `CubeExists`, learns the actual `ToolMount.Top` only when it lies inside the marker's tool region, and stores its exact identity and pose. A previously unseen parked tool appears as **unlearned** in `ToolInfo`; mount it once and run `ToolScan`. A hidden top's occupancy alone cannot establish its identity or orientation. The support merges remain locked until automatic pickup verifies the actual attached top and coupling pose.

If the current scan reports a marker name followed by `Cached top outside tool`, check that its marker, top and merges are on the same connected rigid tool region. If it reports `Top attached to ...` or `Tool attached elsewhere`, the top is still attached to another base. `[Tools] Mount` must name the exact actuated base on this arm. Tops themselves need no name.

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

## Tool operations and recovery

The parked-tool scan walks occupied cells from the marker and stops only across adjacent opposed merge faces: head merge **><** stand merge. Parallel/non-facing merges are not barriers. After one setup mount, this region and the saved coupling pose locate the learned top even when the parked tool shares the stand grid. Pickup must still verify its exact identity through the real attached top before releasing support.

`Tool Head 2` or `Tool 2` parks the current tool on its stand, verifies all selected support ports, detaches the configured mount, approaches and attaches the selected incoming top, then disables only that tool's head-side merges. It verifies exact reciprocal attachment, coupling pose and actual grid separation before rebuilding. `Park` uses generic park ports; `Park 2` selects a primary enumerated port. `ToolInfo` reports state.

Completion stays **OFF**; run `On` to resume manual control. Pilot input, `SwapCancel`, `Stop` or `Off` cancels automatic motion and retains the existing mechanical/support state. After cancellation or interruption, explicitly run `ToolScan` to reconcile the stopped setup. Passive startup scanning does not clear recovery.

Automatic operations require proven compatible support. Unknown/modded or flipped couplers and incompatible attachment geometry are refused. **There is no collision checking or path planning**; approach clearance does not guarantee a clear route. Volumetric restrictions remain future work.

See [docs/RELEASE_NOTES.txt](docs/RELEASE_NOTES.txt) for v2.5 details and [docs/HISTORY.md](docs/HISTORY.md) for retrieving older versions from Git tags. [examples/CustomData.ini](examples/CustomData.ini) shows a generated ten-joint configuration; generate your own identity table rather than copying its populated actuator rows.

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

Edit the fragments in `src/`. The build regenerates `AutoArm_Source.txt` and `AutoArm_Compact.txt`, checks C# 6 against installed SE APIs, verifies token round-trip and identical method IL, and runs the simulated PB harness. Tests also regenerate `examples/CustomData.ini`. [`src/AutoArm.Seam.md`](src/AutoArm.Seam.md) describes the implementation boundaries.

The current compact script has 99,772 UTF-16 characters, leaving 228 under the PB's 100,000-character limit; a longer `ArmName` consumes some of that allowance. UTF-8 file bytes are not the PB's character count. The generated implementation uses short identifiers and keeps the editable `ArmName` header readable. Configured velocity and acceleration caps are not physical momentum measurements. Available torque/force, travel limits and game physics determine achievable motion.

The v2.5 suite passes **174,768 assertions**. Coverage includes topology discovery, signed parallel groups, additive/manual-priority input, gravity bias, movement frames, facing-merge boundaries, swap/park sequences, hidden non-terminal tops, learned couplers across restart, cache scope/geometry, wrong tops, actual coupling-pose gates, cancellation/restart, startup drive stopping and Home attachment faults. Live SE physics, actual instruction costs and loaded merge timing remain unverified.

## Acknowledgements

Development began with a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS). Since then, AutoArm has developed its own automatic discovery and task-space control architecture.
