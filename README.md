# AutoArm

AutoArm is a mechanical-arm control program for the Space Engineers programmable block. It simplifies setup of robotic arms to configuring the start and endpoint of an arm, while allowing later fine tuning of parameters via CustomData within the programmable block.

**To install AutoArm 2.5, follow the [v2.5 installation guide](https://github.com/PommesMitChips/AutoArm/blob/release/v2.5-tool-head-swap/README.md#install-autoarm-25) and download its complete [compact script](https://github.com/PommesMitChips/AutoArm/blob/release/v2.5-tool-head-swap/AutoArm_Compact.txt).** The guide covers simple arms, one-tool parking and multi-tool swapping. Run `Version` in-game to confirm 2.5. Its [PR #1](https://github.com/PommesMitChips/AutoArm/pull/1) remains open; this `main` script is v2.4.

For the v2.4 script on this branch, set `ArmName` in code, name the base actuator `<Arm name> - Base*` and the head reference `<Arm name> - Head*`, then follow its [tagged release notes](https://github.com/PommesMitChips/AutoArm/blob/v2.4/releases/v2.4/AutoArm_v2_4_Notes.txt). The script starts OFF. Run `Check`, then `On` when setup is ready. Stop the old script before replacing it.

For ordinary discovery there must be **exactly one** mechanical name beginning `Arm 1 - Base` and **exactly one** terminal-block name beginning `Arm 1 - Head`. For example, use `Arm 1 - Base - Rotor` and `Arm 1 - Head - Drill`; name other parts `Reach Piston`, `Arm 1 - ToolMerge 1`, etc. The `*` above describes a prefix rule, not a character to type. `Arm 1 - Head 1 - Drill`, `Arm 1 - Head 1 - HeadMerge`, and `Arm 1 - Head 2 - Welder` all match the same head prefix, causing “Head*; found 6” if six parts use it. A multi-tool setup needs v2.5 with `[Tools] Enabled=true` and explicitly configured full marker names; see the linked recipes before renaming the tool assemblies.

Pistons, rotors and hinges can appear throughout a serial arm, including spatially offset or differently oriented stages. Compatible equal-stage parallel branches are synchronized; opposite-facing coaxial rotary pairs receive signed commands. General closed linkages are unsupported. There is no collision avoidance or collision-free homing guarantee.

## Acknowledgements

Development began with a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS). Since then, AutoArm has developed its own automatic discovery and task-space control architecture.
