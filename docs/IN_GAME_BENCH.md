# Test the surveyed two-arm build

This runner is configured for the two arms in the submitted survey. Arm 1 has six stages; Arm 2 has nine. Both arms have two fully retracted pistons. Arm 1's useful translation plane with its head orientation held is outward and sideways. The comparison uses that plane for both arms, while each keeps its own starting head orientation.

The runner submits paths to one Arm PB. It never writes joint velocities, switches equipment or runs `On`. Collision PB stays in charge of clearance. Arms run **one at a time**; the other arm continues holding its head pose.

## Install

1. Stop the Arm PB and replace its code with **v5.0.1** [AutoArm_Compact.txt](../AutoArm_Compact.txt). Keep its Custom Data. The update adds live Safety status to service replies; it does not change PID tuning. Install the updated [Collision script](../AutoArm_Collision_Compact.txt) too for overlap diagnostics with block IDs. Keep Collision Custom Data. Existing ToolSwap scripts remain compatible.
2. Name another PB **`Arm Bench`** and paste the complete [AutoArm_Bench.txt](../AutoArm_Bench.txt) into it. This file is already ready to paste, without compression.
3. In the Arm PB's existing `[global]` section, add the planner, preserving other peers:

   ```ini
   Peers=
   |Collision PB | Safety
   |Arm Survey | Observe
   |Arm Bench | Plan
   ```

   If either arm has its own `Peers`, add the entries to that overriding list instead.
4. Keep Collision PB running. Run `Rescan` on it if you have changed ship blocks since its scan. Restore the arm poses used for the survey, then run `On(Arm 1)` and `On(Arm 2)` on the Arm PB. Leave cockpit controls neutral and the ship stationary throughout the test.

The runner's generated settings already contain your Arm PB and reference block IDs and the surveyed starting poses. You do not need to enter coordinates. Those IDs must still refer to the original blocks. The reference is the first arm's base block on the hull. Paths use its frame, so moving the whole build to another world location does not require new world coordinates.

## Run

Run these commands on **Arm Bench**:

1. **`Check`** verifies both arms are ON and idle, their poses match the survey within 3 cm / 0.5°, and ArmService reports fresh Safety permission. It sends no motion.
2. **`Smoke`** moves Arm 1 outward **10 cm** and back, waits one second, then repeats for Arm 2. Each successful path returns to the pose captured immediately before the test.
3. After a successful smoke test, **`Run`** performs the shared-plane sequence below, first for Arm 1 and then Arm 2.

| Waypoint | Outward displacement | Sideways displacement |
| --- | ---: | ---: |
| 1 | 25 cm | 0 |
| 2 | 25 cm | +10 cm |
| 3 | 25 cm | −10 cm |
| 4 | 25 cm | 0 |
| 5 | 0 | 0 — starting head pose |

All movements are straight head paths with orientation held. Speed is at most **0.05 m/s**, or the arm's configured head speed when that is lower. The normal arm joint speed/acceleration limits and Safety restrictions still apply. No depth translation is requested.

`Cancel` requests Stop for the runner's active path only. `Info` displays current progress. An external Stop, cockpit cancellation, communication loss, prolonged Safety hold or failed return check aborts the test. The runner **does not restart an arm or force an automatic return after an abort**. Check Arm PB status before doing anything else. A successful run verifies both heads remain within 1 cm / 0.3° of their captured starting poses after the final hold.

Copy **all Arm Bench Custom Data** after completion or an abort and send it back. Its report follows `---`. It includes requested waypoints, sampled head poses, control errors, Safety holds/limits, solver budget observations, return errors and elapsed time. Sampling is approximately 10 Hz per arm; it can miss faster vibration. Control error is the controller's tracking error, not a claim about physical clearance.

Replaying the supplied survey through the conservative occupied-cell geometry flags each base rotor against the following hinge. This is a replay result, not a live physics observation. `Check` may therefore report an overlapping-geometry hold on this build before any movement. Send that Check report with the overlapping block IDs; the runner keeps Safety enabled and does not exempt those volumes. Establishing suitable mechanical-interface geometry remains separate from running the motion test.

For repeat testing, leave all Arm/Collision settings unchanged. `Run` captures one start per arm and replays those same paths when `Repeats` is increased. Comparing runs after different PID, speed, geometry or clearance changes is a separate experiment. Returning a head pose does not guarantee that redundant joints return to their original angles.

## Settings

Arm Bench generates `[Bench]` with `Format=1`, `ArmPB`, `Reference`, `Arms`, `Extent=0.25`, `Side=0.1`, `Speed=0.05` and `Repeats=1`. Distances are metres. Accepted ranges: outward extent greater than zero and at most 0.5, side 0..0.2, speed greater than zero and at most 0.1, repeats 1..3. Leave the defaults for the first comparison. `[Baseline Arm 1]` and `[Baseline Arm 2]` contain the surveyed poses; do not edit them to bypass a mismatch.

The total timeout is six minutes. Eight seconds without measured progress while Safety is holding aborts the path; other lack of progress is limited to 35 seconds. Test paths include their return waypoint before movement starts, but emergency stops and clearance restrictions take precedence over completion.
