# Self-collision pair-filter prototype

This is a separate experimental Collision PB script built from the current production Collision code. It recognises segment pairs whose reachable enclosures remain separated across the permitted joint ranges. It does not implement route planning or independently alter the negotiated constraint limit. The v5.0.4 base supports sixteen rows with updated Arm PBs and eight for legacy clients; update Arm PB too when testing that capacity fix.

## Try it

1. With the arms idle, replace Collision PB code with the complete [AutoArm_Collision_Prototype_Compact.txt](../../experimental/AutoArm_Collision_Prototype_Compact.txt). Keep its Custom Data and the Arm PB's Safety peer.
2. Add `SelfPairs=Shadow` to the existing `[Collision]` section and run `Reload` on Collision PB. Shadow is also the default when this key is absent. It runs all the original collision checks and reports eligible exclusions.
3. Run `On(Arm 1)` and `On(Arm 2)` on Arm PB, then `Check` and `Smoke` on Arm Bench. Leave the existing movement/clearance settings unchanged.
4. Copy Collision `Info` after settling and after movement. The added line reports proven/total self pairs and eligible/skipped pairs for the last arm request. Each arm has its own cache. `0/36` means no pair has been proven safe, not that the arm has no self-collision protection.
5. To test actual exclusions, set `SelfPairs=On`, run Collision `Reload`, then repeat the same Arm/Bench commands. `SelfPairs=Off` disables the analysis and exclusions. Accepted mode names are exactly `Off`, `Shadow` and `On`.

An enabled filter keeps the original checks for uncertain/unprocessed pairs and all ship/other-arm comparisons. It checks live geometry remains inside each proven enclosure before skipping a pair. Loss of guidance retains the normal Safety hold. The original scan, clearance, contact and query-budget guards remain.

Run Collision `Rescan` after editing blocks, including edits that do not change grid bounds. The prototype does not add automatic interior-occupancy detection. To revert, paste the production [AutoArm_Collision_Compact.txt](../../experimental/AutoArm_Collision_Compact.txt) and run `Reload`; the extra SelfPairs key can be removed.

## What it proves

The prototype uses actual joint pivots/axes and occupied-grid bounds; no joint-letter sequence grants an exemption. It works in the lowest common ancestor's local frame, removing joints which rigidly carry both segments. Thus globally rotated grids, offset pivots and non-collinear axes require no player configuration.

For a segment centre, each relevant revolute joint contributes a conservative displacement bound `2 * sin(min(pi, maximum angular excursion) / 2) * perpendicular distance to axis`. Each piston contributes its maximum extension change from the proof's reference pose. Downstream error balls retain their radius under upstream rigid rotations, so adding these contributions encloses the product of transforms. The enclosure also contains the full grid bounding-box circumsphere, 5 cm slack and outward numerical padding. Rotating segment geometry is covered by that sphere rather than assumed stationary.

Two enclosures are certified only when their minimum separation exceeds Influence/Clearance plus slack. At use, the gap must also exceed the current live prediction range, and actual occupied-grid bounding spheres must remain inside their enclosures. Joint limits, attachment/top identity, joint-local pivot/axis, grid bounds/scale, topology/contact fingerprint and rescan revision invalidate certificates. Whole-ship rigid motion and ordinary in-range joint movement do not require rebuilding them.

Proof work processes at most two new pairs per request and stops when instruction headroom is insufficient. Unsupported parallel/branched closures, reversed edges, mounted-body paths absent from the group graph, invalid data and uncertain intervals retain original checks. Unlimited or ambiguous wrapped rotary limits use a full-turn bound. Commanded zero speed and Rotor Lock do not imply zero geometric travel. Sampling is used to test the implementation, never to grant an exclusion.

These are conservative sphere bounds in the current kinematic/occupied-cell model. They are deliberately coarse and ignore some correlations between joints. The live enclosure guard also detects departures from that model; it is not a continuous swept-volume or rigorous physical-flex/braking guarantee. The existing limitations on animated telescoping geometry and unseen occupancy edits remain.

## Prototype results

- Source and compact scripts pass installed SE C#6/type-safety/memory-safe compilation and token/IL packing checks. Current compact size: **30,378 UTF-16 characters**.
- **21,541 assertions per artifact** exercise the filter, including 1,200 combined configurations of a rotated, offset, non-collinear PHHP fixture with all occupied cube corners checked against the analytic enclosures, plus 120 full-turn invariant-centre configurations. Cases cover Shadow/On/Off, foldable full-rotation chains, shared piston/rotor movement, wrapped angles, prediction-range guard, geometry/limit/attachment/enclosure changes, budget refusal and unsupported parallel/reversed-edge fallback.
- The v5.0.4 full production suite passes 198,493 assertions, including the capacity boundary and legacy compatibility. The filter's focused source/compact cases remain separate from that suite.
- The current exact compact artifact passes native ResourceMonitoringRewriter/IlInjector replay in On mode: **18,163 / 50,000 instructions** peak across 4,354 invocations. Both Smoke paths complete; nine rows are retained with zero capacity holds. The production v5.0.4 Collision peak is 17,556. Proxy APIs and model AABBs do not establish live physics or wall-clock/API costs.
- The captured build still yields **0/15 proven pairs for Arm 1 and 0/36 for Arm 2**. Consequently no exclusions are applied and no speedup from the self-pair filter is claimed. The capacity fix comes from the v5.0.4 base, not geometric exclusions. Tighter swept-volume bounds or subdivided motion domains are needed to establish more exclusions. The larger existing Run still aborts conservatively in the ideal model.

Build with `Build.ps1 -Track experimental -Scripts CollisionSelfPairs`. Run focused tests with `dotnet run --project tools/ArmTests -- AutoArm_Collision_Prototype_Source.txt --self-pairs` (or the compact filename). Replay a captured survey with `dotnet run --project tools/ArmTests -- AutoArm_Source.txt --self-profile <survey.txt> AutoArm_Collision_Prototype_Compact.txt On`. Logs remain under ignored tool output directories.
