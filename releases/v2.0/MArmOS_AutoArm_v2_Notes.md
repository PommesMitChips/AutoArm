# MArmOS AutoArm v2

The game uses **`MArmOS_AutoArm_v2_Compact.txt`**. Paste the complete text into the programmable-block editor. The readable combined source is `MArmOS_AutoArm_v2_Source.txt`; the maintainable source fragments are in `src/`. The previous fixed-arm v1.9 files remain available.

| Delivered text | Characters |
| --- | ---: |
| Previous v1.9 compact script | 81,784 |
| AutoArm v2 compact script | **45,140** |
| Reduction from v1.9 | **36,644 (44.8%)** |
| Remaining to 100,000 | **54,860** |
| AutoArm v2 readable combined source | 77,447 |

Counts include whitespace and use the delivered LF line endings. The final local run passed **3,995 regression assertions**, C# 6/API checks, and the readable-to-compact method-code equivalence check. The three agents completed their resumed reviews; no known delivery blocker remains within the supported topology scope.

## Set up an arm

1. Set the single required code setting at the top:

   ```csharp
   const string ArmName = "Arm 1";
   ```

2. Name **one** attached base piston, rotor or hinge `Arm 1 - Base - Rotor` (anything beginning `Arm 1 - Base` works). Name **one** head reference block `Arm 1 - Head - Drill` (anything beginning `Arm 1 - Head` works). Other joints need no special names. Even a paired base needs only one marker.
3. Paste the script into the arm's programmable block. It discovers the mechanical route, writes configuration into that programmable block's Custom Data, and waits OFF for two stable stopped scans. The head marker's **block origin** is the controlled centre of rotation; its Forward/Up directions define the tool orientation.
4. Review the generated groups. Edit their existing `Translation` and `Orientation` entries, then run `Reload`. Run `Check`, then `On`.

Each programmable block controls one named arm. Pilot input is taken from an occupied ship controller on the base block's grid. W/S, A/D and Space/C move in the measured head frame; Q/E roll about the head's forward axis. Stored pose targets move with the base grid, including when the ship moves.

After rebuilding or reconnecting the arm, run `Rescan`. After editing Custom Data, run `Reload`. Both stop the arm; run `On` to resume. An invalid configuration is reported without replacing its text.

## What discovery understands

- Rigid grid segments connected by pistons, rotors and hinges, using live block transforms rather than calibrated lengths and hard-coded joint arrays.
- Serial joints and parallel joints sharing the same two rigid segments.
- Separate branches with intermediate grids that **rejoin farther up**. Corresponding stages may mix pistons and rotary joints: for example, piston → hinge → piston on each branch. The original two-stage parallel piston rails and paired base hinges are covered.
- Signed counterpart joints, including opposite-facing coaxial hinges. Corresponding actuators receive coordinated signed commands with a common velocity interval limited by every member.
- Dangling accessory mechanisms outside the base-to-head route are reported and left alone. A reconnection into the arm's route is treated as a topology change.

The supported parallel branches have matching stage counts and joint types, with corresponding motion axes compatible with **1:1 synchronized motion**. Rotary counterparts must share a coaxial motion line; merely having parallel offset axes is insufficient. Unequal branches, arbitrary four-bar mechanisms, variable-ratio linkages, and nested/cross-linked paths outside this model are rejected with a diagnostic. The script does not independently drive an unsupported closed loop.

Acceptance tolerances are approximately 0.5° of axis alignment and 0.03 m of linear-twist difference per radian for rotary counterparts. Those are numerical/physical tolerances, not a proof that loosely aligned mechanisms are interchangeable. Compatibility is checked again during control. Relative pair motion is monitored with 0.25° rotary and 0.02 m piston tolerances, allowing different installed zero positions and rotary wraparound.

The arm is bounded to 24 logical groups and 48 controlled actuators. A separate 128-edge bound applies to the moving component examined for discovery; unrelated mechanisms reached only through the base hull are outside that component. Inventory is capped at 4,096 terminal blocks. Guarded discovery/validation loops reserve instruction budget for stopping owned actuators; actual in-game instruction cost remains unmeasured.

## Custom Data

`[MArmOS]` describes the detected arm and rigid grids. Each `[MArmOS Group NN]` lists the physical members, signs and a generated signature, together with:

```ini
Translation=1
Orientation=1
```

Edit those values **in the generated section**, keeping its other entries. Values run from 0 to 10: 1 is normal participation; larger values make a group more preferred for that task. A zero excludes it from the corresponding pure task. These are allocation preferences, not percentages, torque settings or rotor locks. Every selected joint's complete position and orientation effect stays in the solver.

Corresponding parallel members form one logical group. Giving each rail independent preferences would undermine their mechanical coupling. Group preferences survive rescans only when the physical member/sign/endpoint signature matches. Existing unrelated valid INI sections are preserved. Missing, invalid or conflicting values for a matching group are refused.

## Responsive correction

Every active pose-control tick constructs:

```text
requested head velocity = live pilot velocity + bounded pose correction
```

The same rule applies to angular velocity. There is no old 4 cm input gate and no recovery state that mutes the controls. `Info` exposes pilot, feedback and total requests separately.

Joint speed, travel, acceleration and paired-member bounds still limit the result. Target advancement accounts for the allocated pilot contribution so unreachable travel does not accumulate indefinitely. Equal opposing pilot/correction vectors can sum to zero; pilot target advancement still responds in that case. PI position feedback remains bounded and uses allocation-aware anti-windup; optional derivative gains default to zero.

Default translation is 0.20 m/s, turning 5°/s, rotary cap 0.50 RPM and piston cap 0.20 m/s. Acceleration and travel-braking bounds shape joint commands. Pose error is displayed and corrected; v2 does not reproduce v1.8.2's position-distance and recovery-timeout shutdown policy. Invalid topology, state, configuration, numerical results, stale control timing and exhausted calculation budget stop motion.

## Commands

| Purpose | Run arguments |
| --- | --- |
| Start/stop | `On`, `Off`, `Stop`, `Toggle`, `Hold` |
| Discovery/configuration | `Check`, `Rescan`, `Reload`, `Info`, `Joints`, `Settings`, `Version` |
| Speed | `Speed 0.2`, `TurnSpeed 5`, `JointSpeed 0.5`, `PistonSpeed 0.2` |
| Facing | `Face Forward`, `Face Back`, `Face Left`, `Face Right`, `Face Up`, `Face Down` |
| Angular steps | `Yaw 5`, `Pitch -5`, `Roll 5`, `StepAngle 5`, `Step Yaw 5` |
| Saved joint home | `SetHome`, `GoHome`, `HomeInfo`, `HomeSpeed 0.5 0.2` |

Toolbar arguments 1–9 retain the earlier pitch/facing/yaw actions. `Hold` cancels homing and captures the current head pose. A Face/Step request during homing switches back to pose control before applying the new target. Pilot input cancels GoHome.

Home is keyed to the discovered topology and physical member identities in Storage. Unsupported legacy Storage is left untouched until an explicit `SetHome`. Rebuilding a group can invalidate its old home. GoHome observes physical joint limits and resolves legal rotary wraparound; it has no collision route planner. Paired limited rotors can conservatively refuse ambiguous equivalent-angle returns: there is no general group-level multi-turn route search. Unlimited counterparts follow the branch required by a limited member.

The legacy calibrated `cPose` tree, Legacy mode, scripted Move/MoveTo/RelMove and panel-program execution have been removed. The old historical trace recorder is also absent: `DumpTrace` reports current diagnostics and **does not overwrite configuration**. `TraceClear` states that no history is retained.

Volumetric restrictions are still future work.

## Build and verification

For development, edit the readable fragments in `src/`, then run:

```powershell
.\tools\Build-AutoArm.ps1 -Test
```

This regenerates both combined text files, checks C# 6 and the installed Space Engineers API, verifies that compaction preserves emitted method code and string literals, and runs the regression harness. The local tools require the .NET 10 SDK and the installed game assemblies; they are not required to paste or use the script. They do not modify a game installation or save.

The tests use the real API interfaces with synthetic block graphs, plus an ideal single-piston velocity model. They cover the original arm topology, mixed split/rejoin branches, signed pairs, settings recovery, additive inputs, actuator bounds, faults, home handling, and orientation edge cases. They do **not** simulate Space Engineers constraint physics, mass, lag, collisions, or the game's actual instruction instrumentation. Passing them is not an in-game smoothness or performance measurement.

## Design review

The two GPT-5.6-sol agents debated general closed-linkage solving versus validated synchronized branch groups. The implemented design uses the latter: it provides explicit support for stabilizing parallel mechanisms without concealing incompatible geometry behind approximate loop penalties. They also retained bounded integral correction and removed the old input governor.

A separate GPT-6-Astra adversarial review challenged both agents directly. Confirmed findings became fixes and regression cases, including exact 180° turn axes, rotary branch-cut synchronization, reload after invalid startup data, invalid-config motion entry, and Hold during GoHome.

The last mixed-limit Home test failure was an incorrect test expectation: a bounded rotor just above a half-turn correctly chose the nearer legal target at 2π. The final tests cover both legal directions and a restricted range that forces return to zero.

Actual excerpts from the review messages:

> “I have a confirmed correctness problem in RotationError near 180 degrees, and I am frustrated that the advertised hostile tests missed such a basic Face Back case.” — to the controller agent

> “I am frustrated to see this basic circular-angle seam left unproved.” — to the topology agent

> “Do not claim budget safety from source compaction or MaxMembers alone.” — to the topology agent

> “I am not accepting a reserve guard placed outside a potentially larger-than-reserve block as budget protection.” — to the topology agent
