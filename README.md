# AutoArm

AutoArm is a mechanical-arm control program for the Space Engineers programmable block. It simplifies setup of robotic arms to configuring the start and endpoint of an arm, while allowing later fine tuning of parameters via CustomData within the programmable block.

## Installation

1. Stop your existing arm controller before replacing it.
2. Paste the entire [Arm script](https://github.com/PommesMitChips/AutoArm/blob/v4.0.6/AutoArm_Compact.txt) into an arm programmable block.
3. For tool parking/swapping, paste the [ToolSwap script](https://github.com/PommesMitChips/AutoArm/blob/v4.0.6/AutoArm_ToolSwap_Compact.txt) into a second PB on the same construct. Keep both PBs running. Their names do not matter for automatic setup.
4. Name the parts as shown below. You do not need to edit the code or enter configuration for a quick start.
5. Run `On` on the Arm PB. For another arm, use `On(Arm 2)`. Then use `Select Arm 2` to give it cockpit control.

When upgrading an older setup, delete the old PB Custom Data and let this version generate the new layout. Arm PBs use `[global] Format=7`; ToolSwap PBs use `[global] Format=2`. Incorrect formats produce an error; they are not converted.

## Choose a setup

### Simple arm

Name the first attached joint **`Arm 1 - Base - Rotor`** (or `Arm 1 - Base - Piston` / `Arm 1 - Base - Hinge`). Name exactly one terminal block on the moving end **`Arm 1 - Head - Drill`**, or another description after `Head -`.

Intermediate pistons, rotors and hinges do not need special names. Use only the Arm PB. Run `On` and pilot from your active cockpit.

### Tool parking and swapping

Start with the tools securely merged to their stands. Each tool carries a rotor **base**; the compatible unnamed rotor **top** stays on the arm. Place one or more tool-side merge blocks facing matching stand merges.

If you manually detach a known tool while using the arm, or after a failed swap, AutoArm can rediscover the bare tip and return to cockpit control. It cancels the old swap first. A damaged arm or pending attachment stays stopped; explicit `Stop`, `Off` or `SwapCancel` also prevents automatic recovery.

Name only the arm base and **one reference block per tool**:

| Part | Example name |
| --- | --- |
| First arm joint | `Arm 1 - Base - Rotor` |
| Drill head reference | `Arm 1 - Head 1 - Drill` |
| Welder head reference | `Arm 1 - Head 2 - Welder` |

Leave the rotor bases, rotor top and merge blocks with their ordinary names. Do not give several blocks the same `Head 1` or `Head 2` marker pattern. A reference block can be a drill, welder, grinder or another terminal block rigidly connected to that tool.

Install both scripts and run `On` on the Arm PB. AutoArm finds the tools, rotor bases and facing merge pairs, then saves the setup in each reference block's Custom Data. That data stays with the tool across PB restarts. You do not need to mount every tool once, teach a pose, name the mounts/merges, or fill their names into PB configuration.

The bare arm needs exactly one loose compatible rotor top at its moving end. It may sit directly on the last hinge's moving head. Tool couplers use rotors; ordinary arm joints may use hinges. For first pickup, leave the tool rotor's angle limits unlimited. Rotor Lock is supported.

- `Tool 2` or `Tool Head 2` picks up Head 2, parking the current tool first.
- `Tool(2,Arm 2)` performs the same operation for Arm 2.
- `Park` returns the current tool to its stand and detaches it.
- Successful pickup/swap automatically returns the arm to ON control. Parking leaves it OFF; run `On` if you want to move the bare arm manually.

Tools approach at the arm's normal speed, align in front of the socket and enter in a straight line. Detachment backs out before travel. Start with all tools parked so their existing return orientations are preserved. Keep routes clear: AutoArm does not avoid ship geometry.

If a tool has more than one possible rotor base in its rigid region, or two reference markers describe the same tool, scanning refuses the ambiguous setup. The reference, coupler and tool merges must belong to the same connected tool body.

## Use and features

AutoArm supports mixed piston/rotor/hinge arms, offset joints, compatible parallel actuators, opposite-facing rotary pairs, position/orientation hold, saved Home positions, ordered motion paths and automatic tool parking/swapping.

Tool changes pause briefly while connections settle, then restore arm control automatically. `Tool ready; waiting for arm ON confirmation` means the handoff is pending. `Tool ready; arm ON` confirms the arm has resumed. A failed handoff reports its reason.

Pickup slows at the point in front of the mount, then continues into it. Parking stops slightly short of the stand and enables the head merges to complete docking; a small visible gap at this point is expected.

### Several arms

One Arm PB can host up to eight arms within its instruction budget. Each arm has its own state, targets, joint settings, Home, tool session and faults. Only the selected arm receives cockpit input; the others continue holding or following their commanded paths. Larger setups can distribute arms across more Arm PBs.

Name each arm's base and heads using its own name. `Select Arm 1` or `Select Arm 2` changes live control. Selecting on another Arm PB releases direct cockpit control on the previous PB. Each PB keeps its own selected command target. An arm name must belong to exactly one Arm PB.

Commands without a target go to the selected arm. Add the arm as the last argument to target another:

```text
On(Arm 2)
MoveTo(2,0,1,Arm 2)
OrientTo(1,0,0,0,0,1,Arm 2)
Tool(2,Arm 2)
Stop(Arm 2)
```

The existing toolbar style also works: `MoveTo 2 0 1, Arm 2`. `StopAll` stops every arm in that PB.

One ToolSwap PB can serve several arms, including arms hosted by different Arm PBs. Automatic linking works when each arm has one eligible partner PB. For deliberately split or ambiguous setups, list only that PB's arms in its Custom Data and set `ToolSwapPB` / `Link.ArmPB` explicitly. See [MultiArm.ini](examples/MultiArm.ini) and [MultiArmTools.ini](examples/MultiArmTools.ini).

### Movement views

| Mode | W/S | A/D | C/Space |
| --- | --- | --- | --- |
| `HEAD` | Head Forward/Back | Head Left/Right | Head Down/Up |
| `HRZ` | Cockpit Forward/Back | Cockpit Left/Right | Cockpit Down/Up |
| `VRT` | Cockpit screen Up/Down | Cockpit Left/Right | Into/out of the view |

Translation holds head orientation. Q/E rolls around the head. Optional mouse input controls pitch/yaw. AutoArm uses your active cockpit; it needs no special name.

Set `MovementMode=HEAD`, `HRZ` or `VRT` in Custom Data for the default. `Mode VRT` changes the selected arm's current view; `Mode(VRT,Arm 2)` targets Arm 2. Use `ReadMouse On` / `ReadMouse Off` to enable/disable mouse turning.

### Toolbar commands

Use a programmable block **Run** action with one of these arguments. The optional final arm name applies to every arm command.

| Argument | Action |
| --- | --- |
| `On` | Load settings, discover/scan and enable control |
| `Off` / `Stop` | Stop the target arm |
| `Hold` | Hold its current head pose |
| `Reload` / `Rescan` | Reload settings / rediscover while OFF |
| `Check` / `Info` / `Joints` | Inspect the setup |
| `SetHome` / `GoHome` | Save / return to that arm's Home |
| `MoveTo x y z` | Move while holding orientation |
| `OrientTo fx fy fz ux uy uz` | Turn while holding position |
| `MoveToPosOri x y z fx fy fz ux uy uz` | Set both |
| `Path <pose> \| <pose> ...` | Follow up to 32 poses |
| `Mode HEAD` / `Mode HRZ` / `Mode VRT` | Choose a movement view |
| `ToolScan` / `ToolInfo` | Scan / inspect tools |
| `Tool 2` / `Tool Head 2` | Select a numbered tool |
| `Park` / `SwapCancel` | Park / cancel the target arm's tool operation |

Directional actions include `Forward`, `Back`, `Left`, `Right`, `Up`, `Down`, `PitchUp`, `PitchDown`, `YawLeft`, `YawRight`, `RollLeft` and `RollRight`.

Without a tool, these actions aim the exposed rotor socket. HEAD movement follows that socket's facing direction, and Q/E rolls around it. With a tool attached, controls use the named tool reference block.

`MoveTo` positions are metres from that arm's base in its Forward, Left and Up directions. Orientation commands use those same base directions. `Path` rows use world position, forward and up: nine numbers per pose, separated by `|`. AutoArm reaches each waypoint in order and holds the final pose.

Pilot input cancels an automatic path or tool operation only for the arm receiving that input. `Stop` also cancels it; inspect the equipment, then run `On` to resume. `Reload`, `Check` and `ToolScan` remain available but are not prerequisites for `On`.

### Fine tuning

Custom Data is divided into `[global]` and one section per arm, such as `[Arm 1]` and `[Arm 2]`. Missing settings are generated automatically. Values apply in this order: built-in default, global setting, then arm override. Edit settings and run `On(Arm 2)` on the Arm PB to apply them to both scripts for Arm 2, or `Reload(Arm 2)` to remain OFF.

Use `HeadSpeed` / `HeadTurnSpeed` for movement, `JointSpeed` / `PistonSpeed` for actuator caps, and their acceleration settings to control changes in speed. ToolSwap settings use names such as `Tools.ApproachDistance`, `Tools.MoveSpeed` and `Tools.TurnSpeed`. Travel inherits arm speed; final insertion/withdrawal defaults to 0.05 m/s and 2 degrees/s.

Parking alignment defaults to `Tools.PositionTolerance=0.01` metres and `Tools.AngleTolerance=1` degree. Rotor pickup uses tighter alignment automatically. Parking confirms support before detaching; pickup confirms attachment before releasing support. Existing values in Custom Data take priority over these defaults.

`PositionDamping` and `OrientationDamping` are optional and default to `0` (off). Existing explicit values remain unchanged. Pilot input remains additive, and idle deadbands ignore small movement jitter. If the display says cockpit input is inactive, run `Select <arm name>`; `On` does not take input ownership from another selected arm.

The `Actuators` table gives stage Translation/Orientation preferences from 0 to 10. Put it under `[global]` to share it across structurally identical arms. AutoArm checks joint kinds, parallel member counts and split/rejoin layout; a nonconforming arm reports an error and stays OFF. Different lengths and mounting geometry are allowed and checked separately for each arm. Use local `Actuators` and `Structure` overrides for a different arrangement. Detachable tool couplers are excluded from the shared arm-body template.

Optional helper PBs can plan paths, observe state or stop movement. Add their exact names to `Peers` with `Plan`, `Observe` or `Stop` permissions; see [Modules.ini](examples/Modules.ini). They need a compatible script. The integration guide is [PB modules and paths](docs/MODULES.md).

## Acknowledgements

Development began with a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS). AutoArm now uses its own automatic discovery and task-space control architecture.
