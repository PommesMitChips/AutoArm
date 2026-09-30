# AutoArm

AutoArm is a mechanical-arm control program for the Space Engineers programmable block. It simplifies setup of robotic arms to configuring the start and endpoint of an arm, while allowing later fine tuning of parameters via CustomData within the programmable block.

## Installation

1. Run `Stop` on the previous controller, if one is installed.
2. Copy the entire [AutoArm script](https://github.com/PommesMitChips/AutoArm/blob/v2.5.1/AutoArm_Compact.txt) into the programmable block's script editor.
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

Use [SimpleArm.ini](https://github.com/PommesMitChips/AutoArm/blob/v2.5.1/examples/SimpleArm.ini). Keep `[Tools] Enabled=false`, then run `Reload`, `Check` and `On`.

### One tool with automatic parking

The arm needs a separate detachable end rotor/hinge. Its base stays on the arm; its top stays on the tool. The tool also carries a merge block that faces a matching merge block on its parking support.

| Part | Exact example name |
| --- | --- |
| First arm joint | `Arm 1 - Base - Rotor` |
| Detachable end rotor/hinge base | `Arm 1 - ToolMount` |
| Drill used as the tool reference | `Arm 1 - Head 1 - Drill` |
| Merge carried by the tool | `Arm 1 - Head 1 - HeadMerge` |
| Merge fixed to the parking support | `Arm 1 - ParkMerge 1` |
| Detachable rotor/hinge top | Leave unnamed |

Keep the reference block, merge and top together on the same tool assembly. `ToolMount` must be separate from the first joint marked `Base`.

Use [Park.ini](https://github.com/PommesMitChips/AutoArm/blob/v2.5.1/examples/Park.ini). Begin with the tool attached to `ToolMount`, then run `Reload` or `ToolScan` once and inspect `ToolInfo`.

- `Park` or `Park 1` parks and detaches the tool.
- `Tool Head 1` picks it up again.
- Each operation finishes OFF. Run `On` to resume manual control.

If the tool has several support merges, list every required head-side merge and matching park merge in the configuration.

### Tool swapping

Keep the reference block, compatible rotor/hinge top and head-side merges connected together on each tool. Each stand has matching merge blocks facing the tool's merges.

| Part | Exact example name |
| --- | --- |
| First arm joint | `Arm 1 - Base - Rotor` |
| Detachable end rotor/hinge base | `Arm 1 - ToolMount` |
| Drill tool reference | `Arm 1 - Head 1 - Drill` |
| Drill tool merge | `Arm 1 - Head 1 - HeadMerge` |
| Drill stand merge | `Arm 1 - Head 1 - StandMerge` |
| Welder tool reference | `Arm 1 - Head 2 - Welder` |
| Welder tool merge | `Arm 1 - Head 2 - HeadMerge` |
| Welder stand merge | `Arm 1 - Head 2 - StandMerge` |
| Each tool's detachable top | Leave unnamed |

Use [ToolSwap.ini](https://github.com/PommesMitChips/AutoArm/blob/v2.5.1/examples/ToolSwap.ini). Set `Mount` to the exact name of the end rotor/hinge base and list the complete drill/welder names in `Heads`. The supplied example lists the merges explicitly and provides the command aliases `Head 1` and `Head 2`.

Before the first automatic swap, mount each tool once and run `ToolScan`:

1. Scan the drill while it is attached to `ToolMount`.
2. Keep the controller OFF. Support the drill on its stand before detaching it, then manually attach `ToolMount` to the supported welder and scan again.
3. Confirm the welder is attached before turning off only its head-side merges. Wait for separation from the stand and run `ToolScan` again.
4. Use `ToolInfo` to confirm both tools are ready. Tools shown as `unlearned` still need their setup mount.

This setup is remembered when the same PB is saved or recompiled. Replacing the PB, tool top or reference block, or changing the tool layout, may require repeating it.

Use `Tool Head 2` to park the drill on its stand and mount the welder. Use `Tool Head 1` to switch back. `Tool 1` and `Tool 2` also select them by list order. To add a common parking support, configure its merges under `ParkMerges`.

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
| `Mode HEAD`, `Mode HRZ`, `Mode VRT` | Select a movement view |
| `ToolScan` | Scan configured tools and record a mounted tool |
| `ToolInfo` | Show tool setup and current state |
| `Tool Head 1` or `Tool Head 2` | Select a configured tool |
| `Park` | Park and detach the current tool |
| `SwapCancel` | Cancel an automatic tool operation |

Tool parking and swapping finish OFF. Run `On` afterward to resume manual control. Pilot input or `Stop` cancels automatic movement; inspect the equipment and run `ToolScan` before continuing.

### Fine tuning

Edit `[Config]` in PB Custom Data, then run `Reload`. Use `HeadSpeed` and `HeadTurnSpeed` for head movement, and `JointSpeed` and `PistonSpeed` for actuator speeds. Setting comments show units and accepted ranges.

The `Actuators` table has `Translation` and `Orientation` preferences from 0 to 10. Edit those two values to tune each group's participation in positioning and turning. Additional commands are listed in Custom Data.

## Acknowledgements

Development began with a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS). Since then, AutoArm has developed its own automatic discovery and task-space control architecture.
