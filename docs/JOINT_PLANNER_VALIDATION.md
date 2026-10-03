# Joint planner simulation results — 2026-10-03

These results cover the separate `1.1.0-prototype` Planner and `1.1.0-joint-prototype` Arm. Production scripts and installed Workshop copies are unchanged.

Reproduce with `./tools/Test-JointPlanner.ps1 -Build -Profile` from the repository root. The captured survey is retained in `tools/ArmTests/fixtures/planner-survey.txt`.

| Check | Result |
| --- | --- |
| Readable and actual compressed implementations | Both pass the articulated suite, including 26,706 native-rate frames of obstacle-detour execution. |
| Rotor kinematics | Signed FK, compensating wrist orientation and actual commanded-rate head movement pass. |
| Obstacle detour | Two perpendicular pistons find a 34-configuration detour; an independent per-frame oracle checks the head and shaft against occupied obstacles. |
| Refusal and ownership | Preview emits no movement; blocked goals, interior occupancy edits and scene motion refuse. Local commands release planning holds; lost leases expire; fresh handshakes recover after `On` changes the link generation/epoch. |
| Survey Arm 1 | Six stages; 0.1 m along base Up and return, through the actual Collision service and native-rate plant. Both legs complete within 5 mm of the requested head positions. |
| Survey Arm 2 | Nine stages; same out-and-back trial, through the same owner Arm PB, with the other arm stationary. Both legs complete within 5 mm. |
| Planner instruction counting | Peak 3,032 / 50,000 in the two small articulated fixtures, using the installed SE resource rewriter. This is not a crowded-survey or in-game performance measurement. |
| Existing Arm behavior | The full behavior/integration regression run passes 206,872 assertions. Ordinary configuration, pairing, manual control, ToolSwap, Bench and Collision behavior remain covered. |
| Installed game compiler | Readable and compressed scripts pass C#6, type-safety/memory-safe rewriting and the combined resource-monitoring rewrite including normalized-text reparsing. |
| Pasteable sizes | Arm: 94,974 UTF-16 characters. Planner: 39,980 after syntax compression and unused-field removal. Both fit the PB's 100,000-character limit. |

The surveyed geometry uses reported occupancy and installed model bounds. Physics is an ideal rigid-joint integration of the emitted velocities; gravity, inertia, flex and physical collision response are absent. Clearance is conservative, with bounded allowances for verified native mechanical interfaces.

The round-trip trial caught and drove fixes for shared-motion sweep padding, idle posture movement during planning, route-packet handoff, competing secondary preferences, and piston targets inside the controller's braking buffer. Completion now checks the measured head against the original goal.

The earlier simultaneous `Cross` encounter is **not** claimed solved. This stage validates one moving arm, a stationary peer, and a forced obstacle detour. Larger articulated detours, coordinated arm reservations and in-game physics remain separate validation work. The [prototype guide](JOINT_PLANNER_PROTOTYPE.md) describes setup, commands and boundaries.
