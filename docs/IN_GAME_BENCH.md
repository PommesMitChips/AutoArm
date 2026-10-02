# Test the surveyed two-arm build

This runner is configured for the two arms in the submitted survey. Arm 1 has six stages; Arm 2 has nine. Both arms have two fully retracted pistons. Arm 1's useful translation plane with its head orientation held is outward and sideways. The comparison uses that plane for both arms, while each keeps its own starting head orientation.

The runner submits paths to one Arm PB. It never writes joint velocities, switches equipment or runs `On`. Collision PB stays in charge of clearance. `Smoke` and `Run` move arms one at a time. `Cross` and `Bases` move **both arms concurrently**.

## Install

1. Stop the Arm PB and replace its code with **v5.0.4** [AutoArm_Compact.txt](../AutoArm_Compact.txt). Keep its Custom Data. Install the updated [Collision script](../AutoArm_Collision_Compact.txt) too, keeping Collision Custom Data. Updating both enables sixteen collision constraints and live row-count/capacity telemetry. PID tuning is unchanged. Existing ToolSwap scripts remain compatible.
2. Name another PB **`Arm Bench`** and replace its entire editor contents with [AutoArm_Bench_Compact.txt](../AutoArm_Bench_Compact.txt). Keep existing Custom Data. The [readable source](../AutoArm_Bench.txt) implements the same commands.
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

If an arm has moved and you cannot restore the original pose, replace only Arm Bench's code with the current compact script, keeping Custom Data, and run **`Rebase`** on Arm Bench. Both arms must be ON, ready, idle and unowned, with fresh Safety permission. Leave cockpit input neutral. Rebase observes for one second, requiring less than 5 mm / 0.2 degrees of pose variation, and saves both current head poses as the new comparison baseline. It sends no path or joint commands. Movement restarts the observation window; failure to settle within ten seconds, a Safety hold, cancellation or lost replies leaves the baselines unchanged. Rebase records the previous and captured baselines in its report. It is not a geometry/clearance test or an avoidance pass. Run `Check`, then `Smoke` afterwards.

## Run

Run these commands on **Arm Bench**:

1. **`Check`** verifies both arms are ON and idle, their poses match the saved baseline within 3 cm / 0.5°, and ArmService reports fresh Safety permission. It sends no motion.
2. **`Smoke`** moves Arm 1 outward **10 cm** and back, waits one second, then repeats for Arm 2. Each successful path returns to the pose captured immediately before the test.
3. **`Run`** performs the shared-plane sequence below, first for Arm 1 and then Arm 2. The larger sequence remains conservative/uncompleted in the imported ideal model; a safe abort is not an avoidance-completion pass.

| Waypoint | Outward displacement | Sideways displacement |
| --- | ---: | ---: |
| 1 | 25 cm | 0 |
| 2 | 25 cm | +10 cm |
| 3 | 25 cm | −10 cm |
| 4 | 25 cm | 0 |
| 5 | 0 | 0 — starting head pose |

All movements are straight head paths with orientation held. Speed is at most **0.05 m/s**, or the arm's configured head speed when that is lower. The normal arm joint speed/acceleration limits and Safety restrictions still apply. No depth translation is requested.

`Cancel` requests Stop for the runner's submitted paths only: both arms in a concurrent test. `Info` displays current progress. An external Stop, cockpit cancellation, communication loss, prolonged Safety hold or failed return check aborts the test. Failure on either concurrent arm stops both submitted paths. The runner **does not restart an arm or force an automatic return after an abort**. Check Arm PB status before doing anything else. A successful run verifies both heads remain within 1 cm / 0.3° of their captured starting poses after the final hold.

Progress shows Safety WAIT/HOLD/LIMITED/READY, the accepted joint-command scale, row count/capacity and collision reason. Fresh guidance can still scale motion to zero; it is not permission to move at full speed. `Head net speed` estimates the displacement of the reported head over roughly one second, relative to the reference block; it is not instantaneous velocity and can hide oscillation. `1/2` in Smoke means the outward waypoint completed and the arm is returning. Centimetres of remaining travel can become visually imperceptible when Safety restricts the candidate or the controller approaches its target.

## Larger encounters

Replace only the **Arm Bench** code with the current [AutoArm_Bench.txt](../AutoArm_Bench.txt). Keep its Custom Data, Arm/Collision code, and PID settings. The new optional settings have defaults even when absent from existing data.

Run `Check`, then `PreviewCross`. Preview records the requested waypoints in the report and sends no movement. `Cross` asks both heads to move past the opposite head's starting position by **1 metre**, then return. On the supplied survey this is approximately **19.4 metres each way** per head. Each retains its own orientation and depth coordinate. Both paths are submitted in the same Bench invocation, but motion starts when each ArmService context accepts it; they are not synchronized in lockstep. They can finish and start their return at different times.

For the base encounter, use `PreviewBases`, then `Bases`. Both heads travel beside the opposite arm's base, lower to **7.5 metres above that base in the reference frame**, sweep from **3 metres on one side to 3 metres on the other**, then retrace and return. The default round-trip distances are approximately 77 and 83 metres. This deliberately tests passage around the other arm's structure and neighbouring ship geometry; it does not command either head into a rotor base or the floor. The sweep's nominal height and width do not establish physical clearance for the attached head's volume.

The crossing/head paths use free Cartesian goal pursuit, without the docking line constraint or any contact exemptions. Bench supplies no obstacle detours: the Collision service must influence the arm movement itself. Both heads keep their starting orientation. This can be unreachable for a particular arm, and local clearance assistance can stop in a trap or deadlock instead of finding a route. **Safe hold/abort and successful passage are different results.** Completion and telemetry alone also do not establish that physical blocks never contacted: observe both arms during the test.

The earlier eight-row limit caused Arm 2 to abort both encounters. With v5.0.4's negotiated sixteen-row capacity, the captured Cross/Bases replay reaches nine rows without capacity holds, but both encounters still stall under local clearance limiting. Both paths stop and no forced return is sent. These remain failing encounter tests, not validated autonomous routes. Do not disable Safety or remove obstacles to turn that result into a pass.

Encounter speed defaults to **0.2 m/s**, capped by each arm's configured `HeadSpeed`; normal joint speed/acceleration limits remain active. To override the encounter settings, add these keys to the existing `[Bench]` section:

```ini
EncounterSpeed=0.2
CrossBeyond=1
BaseHeight=7.5
BaseSweep=3
```

Speed accepts 0.001..0.5 m/s; distances are metres, with `CrossBeyond` 0.1..5, `BaseHeight` 2.5..20 and `BaseSweep` 1..10. Height follows the reference base's Up direction, not gravity. Existing `Speed`, `Extent`, `Side` and `Repeats` continue to apply to the small tests; each encounter runs once. Restore the saved starting poses before each separate test, or deliberately use Rebase to begin a new comparison after an abort. A returned head pose can have a different redundant-joint posture. Preview encounter paths again after Rebase: their geometry changes with the starting poses.

Reports retain pose samples at roughly 2 Hz per arm, with Safety reason, command scale and row count/capacity, while summary counters continue at reply frequency. For a long report, use `Page 1`, `Page 2`, etc. after completion. Each page contains at most 12,000 report characters. Send all pages or save the complete Custom Data as a text file; a single clipboard paste can truncate the report. Request Collision `Info` after any hold too.

Copy **all Arm Bench Custom Data** after completion or an abort and send it back. Its report follows `---`. It includes requested waypoints, sampled head poses, control errors, Safety holds/limits, solver budget observations, return errors and elapsed time. Sampling is approximately 2 Hz per arm; it can miss faster vibration. Control error is the controller's tracking error, not a claim about physical clearance.

For the capacity fix, update **both Arm and Collision to v5.0.4**, keeping their Custom Data and Bench settings. Run `Reload` on Collision PB, `On(Arm 1)` and `On(Arm 2)` on Arm PB, then `Check` and `Smoke` on Arm Bench. Send the full report after Smoke or any hold. Collision `Info` now shows rows/capacity alongside tile/candidate/check counts and instruction use. The exact compact Collision replay peaks at 17,556/50,000 instructions and completes both Smoke paths; the larger sideways Run still encounters conservative constraints/timeouts. These results do not establish live SE physics or wall-clock performance.

For repeat testing, leave all Arm/Collision settings unchanged. `Run` captures one start per arm and replays those same paths when `Repeats` is increased. Comparing runs after different PID, speed, geometry or clearance changes is a separate experiment. Returning a head pose does not guarantee that redundant joints return to their original angles.

## Settings

Arm Bench generates `[Bench]` with `Format=1`, `ArmPB`, `Reference`, `Arms`, `Extent=0.25`, `Side=0.1`, `Speed=0.05` and `Repeats=1`. Distances are metres. Accepted ranges: outward extent greater than zero and at most 0.5, side 0..0.2, speed greater than zero and at most 0.1, repeats 1..3. Leave the defaults for the first comparison. `[Baseline Arm 1]` and `[Baseline Arm 2]` contain the saved poses. Use Rebase to capture new ones rather than entering coordinates manually; runs after Rebase belong to a new comparison baseline.

Small tests have a six-minute total timeout. Encounters use twice the longest nominal path duration plus two minutes, capped at one hour. Eight seconds without measured progress while Safety is holding, stale or limiting candidate motion to zero aborts the path; other lack of progress is limited to 35 seconds. ArmService's own waypoint timeouts also apply. Test paths include their return waypoint before movement starts, but emergency stops and clearance restrictions take precedence over completion.
