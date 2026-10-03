# Troubleshooting

## Arm not found or missing head

Check that the base and head use the same arm name:

- Base: `Arm 1 - Base`
- Head: `Arm 1 - Head`

Use one head marker for a fixed tool. For ToolSwap, give each tool a numbered
reference, such as `Arm 1 - Head 1 - Drill` and `Arm 1 - Head 2 - Welder`.
Install ToolSwap in a second programmable block, then run `On` on AutoArm.

## No cockpit input

Run `Select <arm name>` on AutoArm to choose the arm you want to control. Check
that the arm is ON and your cockpit belongs to the same construct. Other arms
can continue automatic movement while you control the selected arm.

## ToolSwap cannot find AutoArm

Both programmable blocks must be on the same construct and have a section for
the arm in Custom Data. They normally find each other automatically.

If there is more than one possible partner, set `ToolSwapPB` in AutoArm's arm
section and `Link.ArmPB` in ToolSwap's arm section to the intended PB names.
See the [configuration examples](examples).

## Tool will not attach

- Start each tool parked on connected merge blocks.
- Keep the numbered head marker, rotor base and tool-side merge on the same rigid
  tool assembly.
- Put one compatible loose rotor head on the arm, with its attachment face clear.
- Use unlimited tool-rotor angle limits for the first pickup.
- Remove extra loose rotor parts if discovery reports an ambiguous mount.

If docking times out, update both AutoArm and ToolSwap, then run `On` again.

## Wrong configuration format

AutoArm uses `Format=7` in `[global]`; ToolSwap uses `Format=2`. Save your preferred
settings, clear incompatible PB Custom Data, let the script regenerate it, then
restore your preferences.

If a tool reports an unsupported format, remove its `AutoArm Tool` section and
scan it again while parked. Keep any unrelated Custom Data.

## Structure mismatch

Check that parallel hinge and piston stages line up and rejoin as intended.
If an arm differs from a shared `Structure` or `Actuators` configuration, correct
the build or give that arm its own settings.

## Motion stops or is unstable

Resolve the fault shown on the programmable block, then run `On` for that arm.
`Stop` stops the selected arm; `StopAll` stops every arm controlled by that PB.

Adjust `HeadSpeed`, `HeadTurnSpeed`, acceleration and damping in AutoArm's Custom
Data. `PositionDamping` and `OrientationDamping` can be set globally or per arm.
ToolSwap's slower movement settings apply during docking and withdrawal.

If the instruction-budget warning appears with several large arms, split them
between programmable blocks.

## Parking or return orientation is wrong

Keep tools merged to their intended stands for the first scan. ToolSwap saves
the parked position and orientation on the named reference block. Scan a tool
again after moving its stand.

Parking tolerances are set in ToolSwap's Custom Data using
`Tools.PositionTolerance` and `Tools.AngleTolerance`.
