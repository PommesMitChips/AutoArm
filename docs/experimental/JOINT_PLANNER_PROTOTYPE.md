# Whole-arm planning prototype

AutoArm still sets up and controls multiple arms in one Arm PB. This prototype adds an optional Planner PB; Collision remains a separate shared service. The Arm PB owns every actuator write, cockpit pairing and motion state. No general command router has been extracted.

This is a simulation prototype, kept separate from the production and Workshop scripts. It plans **one moving arm around a stationary peer**. Simultaneous coordinated passing, automatic ToolSwap routing and moving-obstacle replanning are not implemented here.

## Run the simulation

From the repository root, with the existing development tools and installed Space Engineers assemblies:

```powershell
./tools/Test-JointPlanner.ps1 -Build
```

Add `-Profile` for instruction counting through the installed game's resource rewriter. The runner checks the readable and compressed scripts, an articulated obstacle detour, refusal cases, and the supplied two-arm survey. The survey trial moves each head 0.1 m along its base's Up axis and back to its starting position, through the same Arm PB with the actual Collision service supervising. The other arm is stationary for each trial.

The simulated plant integrates actual commanded rotor, hinge and piston velocities and propagates their rigid transforms. It does not simulate gravity, inertia, flex, rotor torque, contacts or the game's physics engine. A passing simulated route is not an in-game clearance certificate.

## Prototype files

| PB | Pasteable file |
| --- | --- |
| Arm | [AutoArm_JointPlanner_Prototype_Compact.txt](../../experimental/AutoArm_JointPlanner_Prototype_Compact.txt) |
| Planner | [AutoArm_Planner_Prototype_Compact.txt](../../experimental/AutoArm_Planner_Prototype_Compact.txt) |
| Collision | Existing [AutoArm_Collision_Compact.txt](../../experimental/AutoArm_Collision_Compact.txt) |

The production Arm and ToolSwap files are retained for rollback. The prototype Arm omits the optional empirical `PivotProbe` diagnostic to leave space for joint guidance; setup and ordinary arm control remain in that PB. It preserves the existing Arm Custom Data format.

## Optional in-game setup

Stop motion before replacing scripts. Use the prototype Arm file only for this trial, keep the Collision PB, and create a PB named `Planner PB` with the Planner file.

Preserve existing peers in the Arm PB's `[global]` configuration and add the Planner's authority:

```ini
Peers=
|Collision PB | Safety
|Planner PB | Plan
```

The Planner generates this configuration on first run:

```ini
[Planner]
Format=1
ArmPB=
Arm=Arm 1
Clearance=0.15
NodeLimit=256
```

An empty `ArmPB` detects the sole compatible Arm PB. Set its name or `@entityId` if there are several. `Arm` selects one arm within that owner PB. Clearance is in metres; valid values are 0.1–2. `NodeLimit` is 16–1024.

Run `On(Arm 1)` on the Arm PB. Keep other arms physically stationary. On the Planner PB use:

| Command | Action |
| --- | --- |
| `Select Arm 1` | Choose the arm to plan for. |
| `PreviewTo x y z` | Find a checked route without executing it. |
| `MoveTo x y z` | Replan from the current posture, then execute. |
| `Cancel` | Release a planning hold or request a stop of this service's path. |
| `Info` | Show the current phase and route size. |

Coordinates are metres from the named base along its **Forward, Left, Up** axes. The goal retains the current head orientation. A preview is not reused for execution: `MoveTo` captures and checks a fresh scene.

During planning, an authenticated, bounded lease commands the owned joints to zero velocity so idle posture correction does not move the starting configuration. Zero velocity is not a physics lock. If the arm physically drifts outside the captured tolerance, planning refuses motion. Pilot demand or a local motion/settings command releases the hold; loss of the planner expires it. The hold also covers handoff between route packets and is released at completion. Arm control remains ON after successful completion. `Cancel` reports a stop request, not an independently confirmed physical standstill.

## What is checked

The planner first tries a straight head corridor using redundant joint configurations. If that cannot be certified, it tries a head detour using a direct joint route, coordinate-order routes, then a bounded bidirectional joint search. Failure to find a route within the budget means no route was found; it does not prove the destination unreachable.

Each accepted edge encloses the independent joint positions between its endpoints in conservatively expanded geometry. All accepted subdivision points are retained for execution. Shared upstream motion cancels from same-arm pair bounds. Joint limits, parallel signs, disabled joint preferences, head geometry and conservative piston-shaft proxies participate in the checks. The head corridor permits 5 cm deviation; checked orientation remains within approximately 2 degrees. Final measured head tolerance is 5 mm / 0.2 degrees.

The Arm PB treats the joint waypoints as secondary preferences in its existing task solver, with bounds that keep each joint inside the checked segment. These planned preferences replace Collision's idle posture preference for that route; live clearance constraints can still slow or stop delivery. Targets respect the controller's braking margins at physical travel limits. Completion checks the measured head against the original requested goal, including when the ship moves as a rigid whole. A blocked route stops without automatically returning or disabling Safety.

The scene covers occupied cells on the terminal-visible construct: ship geometry, the arm itself and other arms. Unknown geometry uses conservative cell boxes. Static-scene changes, moving peers, detached joints, lost services and exhausted budgets refuse the route. Piston proxies may reject physically possible close approaches.

Every plan starts a fresh occupancy scan. A rolling audit checks 64 cells per tick, including empty cells, for interior edits during planning/execution. This has detection latency proportional to scene size. **Stop, Rescan Collision and replan after building or changing equipment.** Voxels, unrelated grids and instantaneous detection of all geometry edits are outside this prototype.

## Validation scope

The automated checks exercise a two-rotor arm with compensating wrist orientation and a two-piston arm forced around static obstacles. The latter also checks head and shaft clearance with an independent oracle at every native-rate simulation frame. Preview leaves native joints/grids unchanged. Blocked goals, an interior non-terminal edit, scene movement, local cancellation, lost planning leases and renewed handshakes are covered.

The supplied six-stage and nine-stage arms are replayed with their occupied cells and installed model bounds, executing small round trips under live Collision. Larger crossings between those arms remain a separate test; the prototype does not claim that the previously stalled `Cross` encounter is solved.
