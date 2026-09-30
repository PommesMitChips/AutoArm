# AutoArm

Automatic mechanical-arm control for the Space Engineers programmable block.

This snapshot is **v2.4**, covering HEAD, HRZ and VRT cockpit control. Paste the whole [AutoArm_Compact.txt](AutoArm_Compact.txt) into the programmable block; build tools are for development only.

**For automatic parking and tool swapping, use v2.5 from [PR #1](https://github.com/PommesMitChips/AutoArm/pull/1).** Those features are not in this v2.4 `main` script. The [v2.5 setup README](https://github.com/PommesMitChips/AutoArm/blob/release/v2.5-tool-head-swap/README.md#install-and-choose-a-setup) explains exact block names and PB Custom Data for simple arms, one-tool parking, and drill/welder swapping. Run `Version` in-game to check which script you installed.

Set `ArmName` in code, name the base actuator `<Arm name> - Base*` and the head reference `<Arm name> - Head*`, then follow the preserved [release notes](releases/v2.4/AutoArm_v2_4_Notes.txt). The script starts OFF. Run `Check`, then `On` when setup is ready. Stop the old script before replacing it.

For ordinary discovery there must be **exactly one** mechanical name beginning `Arm 1 - Base` and **exactly one** terminal-block name beginning `Arm 1 - Head`. For example, use `Arm 1 - Base - Rotor` and `Arm 1 - Head - Drill`; name other parts `Reach Piston`, `Arm 1 - ToolMerge 1`, etc. The `*` above describes a prefix rule, not a character to type. `Arm 1 - Head 1 - Drill`, `Arm 1 - Head 1 - HeadMerge`, and `Arm 1 - Head 2 - Welder` all match the same head prefix, causing “Head*; found 6” if six parts use it. A multi-tool setup needs v2.5 with `[Tools] Enabled=true` and explicitly configured full marker names; see the linked recipes before renaming the tool assemblies.

Pistons, rotors and hinges can appear throughout a serial arm, including spatially offset or differently oriented stages. Compatible equal-stage parallel branches are synchronized; opposite-facing coaxial rotary pairs receive signed commands. General closed linkages are unsupported. There is no collision avoidance or collision-free homing guarantee.

## Imported history

This Git history was reconstructed from preserved script files. Each import represents a saved snapshot, not an original development commit. Commit dates record the import; no historical timestamps have been invented. Original release files, names and notes are retained under `releases/`.

Development began with a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS). AutoArm uses its own automatic discovery and task-space control architecture. It is not an official MArmOS release or a drop-in fork.
