# AutoArm

Automatic mechanical-arm control for the Space Engineers programmable block.

This snapshot is **v2.0**, covering automatic discovery and additive pose control. Paste the whole [AutoArm_Compact.txt](AutoArm_Compact.txt) into the programmable block; build tools are for development only.

Set `ArmName` in code, name the base actuator `<Arm name> - Base*` and the head reference `<Arm name> - Head*`, then follow the preserved [release notes](releases/v2.0/MArmOS_AutoArm_v2_Notes.md). The script starts OFF. Run `Check`, then `On` when setup is ready. Stop the old script before replacing it.

Pistons, rotors and hinges can appear throughout a serial arm, including spatially offset or differently oriented stages. Compatible equal-stage parallel branches are synchronized; opposite-facing coaxial rotary pairs receive signed commands. General closed linkages are unsupported. There is no collision avoidance or collision-free homing guarantee.

## Imported history

This Git history was reconstructed from preserved script files. Each import represents a saved snapshot, not an original development commit. Commit dates record the import; no historical timestamps have been invented. Original release files, names and notes are retained under `releases/`.

Development began with a mining-arm adaptation inspired by [Philippe117's MArmOS](https://github.com/Philippe117/MArmOS). AutoArm uses its own automatic discovery and task-space control architecture. It is not an official MArmOS release or a drop-in fork.