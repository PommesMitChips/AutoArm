# AutoArm

AutoArm is a mechanical-arm control program for the Space Engineers programmable block. It simplifies setup of robotic arms to configuring the start and endpoint of an arm, while allowing later fine tuning of parameters via CustomData within the programmable block.

## Installation

1. Run `Stop` on the previous controller, if one is installed.
2. Copy the entire [AutoArm script](https://github.com/PommesMitChips/AutoArm/blob/v2.6/AutoArm_Compact.txt) into the programmable block's script editor.
3. Change `const string ArmName = "Arm 1";` to your arm's name, then check/compile it in the game editor.
4. Choose a setup below. Name the parts and put the matching configuration in the **programmable block's Custom Data**.
5. Run `Reload`, wait for setup to finish OFF, then `Check` and `On`.

The examples use `Arm 1`. Replace it with your chosen name throughout the block names and configuration. Match names, spaces and capitalization exactly. The operating cockpit can have any name; AutoArm uses the one you are actively piloting.

For a fresh setup, copy the chosen example into PB Custom Data. When changing an existing setup, edit its existing sections to retain your settings. If the script reports a configuration-format error, save the settings you want to keep, delete Custom Data, and run `Reload` to generate new defaults.

### Simple arm

Use this for one fixed tool without automatic parking or swapping.

| Part | Example name |
| --- | --- |
| First piston, rotor or hinge on the ship | `Arm 1 - Base - Rotor` |
| One drill or camera used as the head reference | `Arm 1 - Head - Drill` |
| Other joints | Any name, such as `Reach Piston` or `Elbow Hinge` |
| Other tools and merges | Names such as `Arm 1 - Drill 2` or `Arm 1 - ToolMerge 1` |

Name only one arm joint with the `Arm 1 - Base` prefix and only one reference block with the `Arm 1 - Head` prefix. Other drills, merges and spare tools must not use that head prefix in this setup.

Use [SimpleArm.ini](https://github.com/PommesMitChips/AutoArm/blob/v2.6/examples/SimpleArm.ini). Keep `[Tools] Enabled=false`, then run `Reload`, `Check` and `On`.

### One tool with automatic parking

For automatic parking and swapping, put the detachable rotor/hinge **base on the tool**, and its unnamed **top on the arm**. Keep an existing terminal block or camera on the arm's end as its reference. The reference stays with the arm when a tool is removed.

| Part | Exact example name |
| --- | --- |
| First arm joint | `Arm 1 - Base - Rotor` |
| Reference block at the arm's end | `Arm 1 - ToolMount` |
| Rotor/hinge base carried by the tool | `Arm 1 - Head 1 - Mount` |
| Drill used as the tool reference | `Arm 1 - Head 1 - Drill` |
| Merge carried by the tool | `Arm 1 - Head 1 - HeadMerge` |
| Merge fixed to the parking support | `Arm 1 - ParkMerge 1` |
| Rotor/hinge top carried by the arm | Leave unnamed |

Keep the tool's reference, base and head merge connected together. The head merge must face the support merge. Avoid another solid connection between tool and support that would prevent separation.

Use [Park.ini](https://github.com/PommesMitChips/AutoArm/blob/v2.6/examples/Park.ini). `[Tools] Mount` names the arm-end reference; `[Tool01] Mount` names the tool's rotor/hinge base. Begin installation with the tool attached, then run `Reload`, wait for the scan to finish, and inspect `ToolInfo`.

- `Park` or `Park 1` parks and detaches the tool.
- `Tool Head 1` picks it up again.
- Each operation finishes OFF. Run `On` to resume manual control.

If the tool has several support merges, list every required head merge and matching park merge in the configuration.

### Tool swapping

Use the same arrangement: one unnamed rotor/hinge top stays on the arm, and each tool carries its own named, compatible rotor/hinge base. Each tool also carries its reference block and head merges; its stand has matching merges facing them.

| Part | Exact example name |
| --- | --- |
| First arm joint | `Arm 1 - Base - Rotor` |
| Reference block at the arm's end | `Arm 1 - ToolMount` |
| Drill tool's rotor/hinge base | `Arm 1 - Head 1 - Mount` |
| Drill tool reference | `Arm 1 - Head 1 - Drill` |
| Drill tool merge | `Arm 1 - Head 1 - HeadMerge` |
| Drill stand merge | `Arm 1 - Head 1 - StandMerge` |
| Welder tool's rotor/hinge base | `Arm 1 - Head 2 - Mount` |
| Welder tool reference | `Arm 1 - Head 2 - Welder` |
| Welder tool merge | `Arm 1 - Head 2 - HeadMerge` |
| Welder stand merge | `Arm 1 - Head 2 - StandMerge` |
| Rotor/hinge top carried by the arm | Leave unnamed |

Use [ToolSwap.ini](https://github.com/PommesMitChips/AutoArm/blob/v2.6/examples/ToolSwap.ini). `[Tools] Mount` names the arm-end reference. Each `[ToolNN] Mount` names that tool's rotor/hinge base. List the complete drill/welder names in `Heads`; their order matches `Tool01`, `Tool02`, and so on. The example lists the merges explicitly and provides the command aliases `Head 1` and `Head 2`.

Start installation with Head 1 attached to the arm and Head 2 secured on its stand. Run `Reload`, wait for the scan to finish, and check `ToolInfo`. Then `Tool 2` or `Tool Head 2` parks Head 1 and picks up Head 2. `Tool 1` or `Tool Head 1` switches back. Each spare tool can be used immediately; you do not need to mount it first or teach a parking position.

The arm-end setup is saved automatically and survives saving or recompiling the same PB, including while the arm is headless. If you replace the PB, arm top or reference block, begin setup with any compatible tool attached. To add a common parking support, configure its merges under `ParkMerges`.

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

Add a programmable-block **Run** action to your toolbar and enter one of these arguments:

| Argument | Action |
| --- | --- |
| `On` | Enable manual arm control |
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
| `Mode HEAD`, `Mode HRZ`, `Mode VRT` | Select a movement view |
| `ToolScan` | Scan configured tools and check their setup |
| `ToolInfo` | Show tool setup and current state |
| `Tool Head 1` or `Tool Head 2` | Select a configured tool |
| `Park` | Park and detach the current tool |
| `SwapCancel` | Cancel an automatic tool operation |

Tool parking and swapping finish OFF. Run `On` afterward to resume manual control. Pilot input or `Stop` cancels automatic movement; inspect the equipment and run `ToolScan` before continuing.

### Position and orientation commands

Run `On` first. `MoveTo` coordinates are metres from the base in its Forward, Left and Up directions. For example, `MoveTo 2 0 1` requests a position two metres forward and one metre up from the base. The head keeps its held orientation.

`OrientTo` takes a desired forward direction followed by an up direction, using the same base directions. For example, `OrientTo 1 0 0 0 0 1` points forward with up aligned to the base. `MoveToPosOri` combines the three position values and six orientation values. Choose reachable targets with a clear path. Live pilot input remains active and adjusts the target; these commands also accept PB Run actions from other automation.

### Fine tuning

Edit `[Config]` in PB Custom Data, then run `Reload`. Use `HeadSpeed` and `HeadTurnSpeed` for head movement, and `JointSpeed` and `PistonSpeed` for actuator speeds. Setting comments show units and accepted ranges.

The `Actuators` table has `Translation` and `Orientation` preferences from 0 to 10. Edit those two values to tune each group's participation in positioning and turning. Additional commands are listed in Custom Data.

## Acknowledgements

Development began with a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS). Since then, AutoArm has developed its own automatic discovery and task-space control architecture.
