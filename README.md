# AutoArm

AutoArm is a mechanical-arm control program for the Space Engineers programmable block. It simplifies setup of robotic arms to configuring the start and endpoint of an arm, while allowing later fine tuning of parameters via CustomData within the programmable block.

## Installation

1. Run `Stop` on the previous controller, if one is installed.
2. Copy the entire [AutoArm script](https://github.com/PommesMitChips/AutoArm/blob/v3.1/AutoArm_Compact.txt) into the arm programmable block's script editor. A simple arm needs only this PB.
3. For parking or tool swapping, install the [ToolSwap script](https://github.com/PommesMitChips/AutoArm/blob/v3.1/AutoArm_ToolSwap_Compact.txt) in a second PB on the same construct. Name the PBs `Arm 1 - Arm PB` and `Arm 1 - ToolSwap PB`.
4. Change `const string ArmName = "Arm 1";` to your arm's name in each installed script, then check/compile them in the game editor.
5. Choose a setup below. Name the parts and put the matching configuration in each **programmable block's Custom Data**.
6. Run `On` once on the **arm PB**. AutoArm loads configuration, discovers/scans and enables control when setup succeeds. Keep both PBs running when tools are configured.

The examples use `Arm 1`. Replace it with your chosen name throughout the scripts, block names and configuration. Match names, spaces and capitalization exactly. The operating cockpit can have any name; AutoArm uses the one you are actively piloting. You can use different PB names if the two configurations name each other exactly.

For a fresh setup, copy the chosen example into PB Custom Data. When changing an existing setup, edit its existing sections to retain your settings. If the script reports a configuration-format error, save the settings you want to keep, delete Custom Data, and run `On` to generate new defaults and start setup.

### Simple arm

Use this for one fixed tool without automatic parking or swapping.

| Part | Example name |
| --- | --- |
| First piston, rotor or hinge on the ship | `Arm 1 - Base - Rotor` |
| One drill or camera used as the head reference | `Arm 1 - Head - Drill` |
| Other joints | Any name, such as `Reach Piston` or `Elbow Hinge` |
| Other tools and merges | Names such as `Arm 1 - Drill 2` or `Arm 1 - ToolMerge 1` |

Name only one arm joint with the `Arm 1 - Base` prefix and only one reference block with the `Arm 1 - Head` prefix. Other drills, merges and spare tools must not use that head prefix in this setup.

Put [SimpleArm.ini](https://github.com/PommesMitChips/AutoArm/blob/v3.1/examples/SimpleArm.ini) in the arm PB. Leave `[Modules] ToolSwapPB` empty, then run `On`. No second PB is needed.

### One tool with automatic parking

For automatic parking and swapping, put the detachable rotor **base on the tool**, and its unnamed **top on the arm**. AutoArm finds the arm-side rotor part automatically. The final arm joint can have any name.

| Part | Exact example name |
| --- | --- |
| First arm joint | `Arm 1 - Base - Rotor` |
| Rotor base carried by the tool | `Arm 1 - Head 1 - Mount` |
| Drill used as the tool reference | `Arm 1 - Head 1 - Drill` |
| Merge carried by the tool | `Arm 1 - Head 1 - HeadMerge` |
| Merge fixed to the parking support | `Arm 1 - ParkMerge 1` |
| Rotor top carried by the arm | Leave unnamed |

Keep the tool's reference, base and head merge connected together. The head merge must face the support merge. Avoid another solid connection between tool and support that would prevent separation.

Start with the tool parked and its merges connected. ToolSwap records its actual position and orientation relative to the stand and remembers that pose across restarts. Returning it preserves that orientation; you do not need to identify the merge blocks' hidden roll. A tool introduced already mounted has no recorded parked pose yet: its first park follows merge geometry, which can rotate the tool by 90°.

Put [ArmWithTools.ini](https://github.com/PommesMitChips/AutoArm/blob/v3.1/examples/ArmWithTools.ini) in the arm PB and [Park.ini](https://github.com/PommesMitChips/AutoArm/blob/v3.1/examples/Park.ini) in the ToolSwap PB. `[Tool01] Mount` names the tool's rotor base. Run `On` on the arm PB and wait for setup to finish. `ToolInfo` shows the detected tools if you want to check them.

- `Park` or `Park 1` parks and detaches the tool.
- `Tool Head 1` picks it up again.
- Pickup automatically resumes manual control. Parking leaves the bare arm OFF; run `On` to move it manually.

If the tool has several support merges, list every required head merge and matching park merge in the configuration.

### Tool swapping

Use the same arrangement: one unnamed rotor top stays on the arm, and each tool carries its own named, compatible rotor base. Each tool also carries its reference block and head merges; its stand has matching merges facing them.

| Part | Exact example name |
| --- | --- |
| First arm joint | `Arm 1 - Base - Rotor` |
| Drill tool's rotor base | `Arm 1 - Head 1 - Mount` |
| Drill tool reference | `Arm 1 - Head 1 - Drill` |
| Drill tool merge | `Arm 1 - Head 1 - HeadMerge` |
| Drill stand merge | `Arm 1 - Head 1 - StandMerge` |
| Welder tool's rotor base | `Arm 1 - Head 2 - Mount` |
| Welder tool reference | `Arm 1 - Head 2 - Welder` |
| Welder tool merge | `Arm 1 - Head 2 - HeadMerge` |
| Welder stand merge | `Arm 1 - Head 2 - StandMerge` |
| Rotor top carried by the arm | Leave unnamed |

Put [ArmWithTools.ini](https://github.com/PommesMitChips/AutoArm/blob/v3.1/examples/ArmWithTools.ini) in the arm PB and [ToolSwap.ini](https://github.com/PommesMitChips/AutoArm/blob/v3.1/examples/ToolSwap.ini) in the ToolSwap PB. Each `[ToolNN] Mount` names that tool's rotor base. List the complete drill/welder names in `Heads`; their order matches `Tool01`, `Tool02`, and so on. The example lists the merges explicitly and provides the command aliases `Head 1` and `Head 2`.

Start with all tools secured on their stands to record their return orientations automatically. The arm can start bare with its unnamed rotor part installed. An already mounted tool is also supported, with the first-park limitation described above. Run `On` and wait for setup to finish. Then `Tool 2` or `Tool Head 2` parks Head 1 and picks up Head 2. `Tool 1` or `Tool Head 1` switches back. Control resumes automatically after the selected tool separates from its stand. Each spare tool can be used immediately; you do not need to mount it first or teach a parking position.

Pickup moves to the configured clearance in front of the rotor socket, aligns, then enters along a straight line with orientation held. Dropping a tool backs the arm out along the socket axis before travelling to the next tool. Expected merge/mount changes pause movement while AutoArm rediscovers the arm; they do not cancel the swap or require another `On`.

AutoArm detects the arm end again after restarting or recompiling, whether a tool is attached or the arm is bare. To add a common parking support, configure its merges under `ParkMerges`.

### Starting with a bare arm

Build one unattached rotor part on the arm's moving end. It can be placed directly on the final hinge's moving head. The hinge does not need a special name, and no camera or reference block is required.

Use the two-PB tool-swap configuration above, keep the selected tool secured on its stand, run `On` on the arm PB, and wait for setup to finish. You can then move the bare arm or run `Tool 2` to approach and pick up Head 2. Manual control resumes automatically after pickup. No offset, direction settings, initial tool mount or teaching is required. The rotor part is the movement focus while the arm is bare; the configured tool marker becomes the focus after pickup.

Keep only one loose rotor part on the moving arm. Its attachment to the arm must be clear: avoid surrounding it with blocks touching several possible mounting faces. AutoArm stops if it cannot identify a unique part and mounting face.

For the first pickup from a bare arm, leave the selected tool rotor's lower and upper angle limits **unlimited**. **Rotor Lock** is supported: AutoArm temporarily unlocks it for that first attachment and restores the requested lock after verification. Tool couplers use rotors; ordinary arm joints can use pistons, rotors and hinges.

## Use and features

AutoArm supports:

- Mixed piston, rotor and hinge arms, including offset and differently oriented joints.
- Compatible parallel actuators, including opposite-facing rotor/hinge pairs sharing a rotation axis.
- Position and orientation hold while moving the head.
- Head, excavator and crane movement views.
- Saved home positions, tool parking and tool swapping.
- Adjustable movement speeds, actuator speed/acceleration limits and control preferences in Custom Data.

Choose clear routes for automatic parking and swapping; AutoArm does not avoid ship geometry.

### Movement views

| Mode | W/S | A/D | C/Space |
| --- | --- | --- | --- |
| `HEAD` | Head Forward/Back | Head Left/Right | Head Down/Up |
| `HRZ` | Cockpit Forward/Back | Cockpit Left/Right | Cockpit Down/Up |
| `VRT` | Cockpit screen Up/Down | Cockpit Left/Right | Into/out of the view |

Translation holds the head's orientation. Q/E rolls around the head. Optional mouse input controls pitch and yaw. These keys assume the usual game bindings.

Set `MovementMode=HEAD`, `HRZ` or `VRT` in `[Config]` for the default view. Use `Mode HEAD`, `Mode HRZ` or `Mode VRT` to change it while operating. Use `ReadMouse On` or `ReadMouse Off` to enable or disable mouse turning.

### Toolbar commands

Add an **arm PB Run** action to your toolbar and enter one of these arguments. Tool commands are forwarded to the ToolSwap PB:

| Argument | Action |
| --- | --- |
| `On` | Load configuration, discover/scan and enable arm control |
| `Off` or `Stop` | Stop arm movement |
| `Hold` | Hold the current head pose |
| `Reload` | Apply Custom Data edits |
| `Check` | Check setup before enabling control |
| `Info` or `Joints` | Show the arm or joint details |
| `SetHome` | Save the current joint positions |
| `GoHome` | Return the attached arm to those positions |
| `MoveTo x y z` | Move to a position while holding orientation |
| `OrientTo fx fy fz ux uy uz` | Turn to an orientation while holding position |
| `MoveToPosOri x y z fx fy fz ux uy uz` | Set both position and orientation |
| `Path <pose> \| <pose> ...` | Follow a list of position/orientation waypoints |
| `Mode HEAD`, `Mode HRZ`, `Mode VRT` | Select a movement view |
| `ToolScan` | Scan configured tools and check their setup |
| `ToolInfo` | Show tool setup and current state |
| `Tool Head 1` or `Tool Head 2` | Select a configured tool |
| `Park` | Park and detach the current tool |
| `SwapCancel` | Cancel an automatic tool operation |

Successful tool pickup and swapping automatically resume manual control. Parking leaves the bare arm OFF. Pilot input or `Stop` cancels automatic movement and leaves control OFF; inspect the equipment, then run `On` to check the setup and resume. `Reload`, `Check` and `ToolScan` remain available as separate setup checks; they are not required before `On`.

### Position and orientation commands

Run `On` first. `MoveTo` coordinates are metres from the base in its Forward, Left and Up directions. For example, `MoveTo 2 0 1` requests a position two metres forward and one metre up from the base. The head keeps its held orientation.

`OrientTo` takes a desired forward direction followed by an up direction, using the same base directions. For example, `OrientTo 1 0 0 0 0 1` points forward with up aligned to the base. `MoveToPosOri` combines the three position values and six orientation values. Choose reachable targets with a clear path. Live pilot input remains active and adjusts the target; these commands also accept PB Run actions from other automation.

`Path` accepts up to 32 poses, separated by `|`. Each pose contains nine numbers: world position `x y z`, forward direction `fx fy fz`, and up direction `ux uy uz`. AutoArm reaches the waypoints in order, reports progress and holds the final pose. `Stop` or live pilot input cancels a path. ToolSwap uses paths automatically for approach and insertion; you do not need to enter its waypoints yourself.

### Fine tuning

Edit `[Config]` in PB Custom Data, then run `On` to apply the settings and resume control. Use `Reload` instead if you want to apply them while staying OFF. Use `HeadSpeed` and `HeadTurnSpeed` for head movement, and `JointSpeed` and `PistonSpeed` for actuator speeds. Setting comments show units and accepted ranges.

The `Actuators` table has `Translation` and `Orientation` preferences from 0 to 10. Edit those two values to tune each group's participation in positioning and turning. Use the toolbar command table above; Custom Data contains settings only.

Arm movement settings belong in the **arm PB**. Tool profiles, merge lists and `[Tools]` settings belong in the **ToolSwap PB**. Travel uses the arm's `HeadSpeed` and `HeadTurnSpeed`. ToolSwap's `MoveSpeed` and `TurnSpeed` apply to final insertion/docking and withdrawal; their slower defaults remain `0.05` m/s and `2`°/s. `ApproachDistance` controls clearance before docking/pickup and backoff after detaching; for example, `2.5` requests 2.5 m. After editing either PB, run `On` on the arm PB to apply the changes. When updating a tool setup, replace both PB scripts together.

## Acknowledgements

Development began with a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS). Since then, AutoArm has developed its own automatic discovery and task-space control architecture.
