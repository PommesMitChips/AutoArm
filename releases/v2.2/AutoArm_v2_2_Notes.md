# AutoArm v2.2 — startup timing, scan cost and pivot diagnostics

Use **`AutoArm_v2_2_Compact.txt`** in the programmable-block editor. It contains **56,732 characters**, with **43,268** remaining below 100,000. The readable combined source is `AutoArm_v2_2_Source.txt`.

The supplied ten-rotary-joint configuration is valid. No participation-weight or topology-name changes are needed to fix the reported `Check` → `On` failure. Existing Format 1 configuration and matching saved homes remain supported.

## Install

Run `Stop` in the old script, replace its text with the complete v2.2 compact file, and check it in the editor. Run `Version` to verify `AutoArm 2.2`, then `Check` and `On`. Keep `ArmName = "Arm 1"` for the supplied configuration. The base/head naming and participation settings are unchanged from v2.1.

## Why Check passed but On stopped

The old controller treated every non-positive `Runtime.TimeSinceLastRun` as a fault. Space Engineers can invoke a terminal/toolbar command in the same simulation frame as a previous invocation, legitimately producing **zero** elapsed time. `Check` did not enter the motion-update path; `On` did, and encountered that guard.

v2.2 applies commands immediately but advances motion only on a timed callback with positive accumulated time. Zero-time calls do not fabricate a 1/60-second step and do not disable On. Stop/Off still zero owned outputs immediately.

Intervals are accumulated across intervening manual calls, because the API reports time since **any** invocation, not since the last control calculation. This also prevents manual calls from hiding a stale control gap. Measured velocities use the complete observation interval; the capped integration/acceleration step is kept separate. Setup requires distinct timed observations, so repeated commands in one frame cannot satisfy its settling checks.

The installed engine implementation uses simulation-frame counts for this property. It is not a wall-clock timer. Negative/invalid intervals and excessive control gaps still stop motion.

## Topology scanning

Every active control pass checks cached known connections, attachment/endpoints/type, marker identity, construct membership and paired-joint synchronization. These checks do not enumerate the terminal system.

A full snapshot is taken every **30 active validation passes**, and explicitly for On, Check, setup, Reload, Rescan, SetHome, GoHome and pivot-probe captures. An observed change to a cached connection stops immediately; the snapshot baseline is never silently replaced.

New/untracked connections or duplicate markers have up to **30 active passes** of detection latency. That is approximately 0.5 simulation seconds with regular Update1 execution, not a hard wall-clock bound. This is the accepted tradeoff for reducing full inventory scans. It is not a count-only cache, and no claim is made that unchanged block counts prove an unchanged graph.

In the deterministic test rig, 60 active passes performed two full inventory scans instead of sixty. Actual in-game timing savings have not been profiled. Instruction-reserve guards remain before scanning and inside bounded work.

## Status output

Routine numeric status construction runs approximately **four times per simulation second**. Space Engineers clears the PB's actual output buffer on each invocation, so v2.2 replays the cached string each time to keep the display populated.

This throttles **formatting and string construction**, not the final Echo call or its engine/network work. Control updates remain at their normal frequency. Command responses, faults, stops and homing transitions are displayed immediately.

`Info`, `Help` and `Joints` pin their output so it is readable instead of being replaced on the next control tick. Run `Status` to resume routine live status. Starting control or recapturing Hold also restores the live display.

## Empirical pivot comparison

The control pivot remains `GetPosition()`. Installed vanilla model inspection supports that axis line; no evidence currently justifies substituting the top-block position or midpoint.

v2.2 adds a **player-operated, passive** comparison of all three hypotheses using actual joint-angle and head-pose changes. It does not initiate motion or automatically select a new pivot. It does not write configuration or Storage. Existing topology-fault handling can still stop owned joints when validation fails.

For the first trial, use a single serial rotary group with room for a small safe movement:

1. Park the ship, stop other moving mechanisms, run `Off`, and let the arm settle. Use `Joints` or Custom Data to identify the group number.
2. Run `PivotProbe Start 1` for group 1, or substitute the desired group number.
3. Manually move **only that group** by approximately 3–10°. Return its target velocity to zero and wait for physical movement to settle. For a parallel group, all members must move with their discovered signs; use a single-member group first if manual synchronization is uncertain.
4. Run `PivotProbe End`.
5. Repeat in the opposite direction. Run `PivotProbe Report` and copy the output for evaluation. `PivotProbe Reset` clears the RAM trials.

Start/End require the controller Off and all owned target velocities zero. The probe refuses insufficient/excessive angular movement, tiny head travel, other-group movement, invalid state, selected locks/near limits, inconsistent paired movement, changed axes or inconsistent head orientation.

For accepted trials it reports base-centre, top-centre and midpoint positional residuals, paired-prediction spread, measured angle/travel and orientation residual. Ship movement is expressed in the live base frame. Axially equivalent candidates normally produce the same prediction and are reported as inconclusive. A smaller residual is **not a calibration result**: flex, load, imperfect settling, motion of ignored accessories and other unmodeled effects can contribute.

Trials are RAM-only, with the latest twelve retained. On, GoHome, Reload and Rescan discard the probe instance so a captured snapshot cannot silently cross those state changes. Off/Stop can be used to stop a manual jog without discarding a pending capture.

**The probe has not yet been run on the user's actual arm.** Real motion samples are still required before making an empirical pivot decision. The new software was tested with known simulated pivot hypotheses; those tests do not substitute for live measurements.

## Verification

`tools/Build-AutoArm.ps1 -Test` passed **17,093 assertions**, local C# 6 / installed-API checks and source-to-compact emitted-code equivalence.

New regression coverage includes:

- The exact ten member IDs, eleven grid IDs and fingerprint supplied with the screenshot.
- Check followed by zero-time On, zero-time timed callbacks, mixed update flags and the next positive control tick.
- Commands interleaved with timed updates, uncapped observation timing, stale-gap detection and same-frame setup attempts.
- Two full scans per sixty active passes, immediate known changes and bounded unknown-crosslink/marker detection.
- Cached output persistence, throttled formatting, pinned Info and truthful Off/homing/tolerance messages.
- Actual-angle finite-motion pivot comparisons, differing base/top/midpoint hypotheses, axis equivalence, rejected invalid trials and absence of successful-probe actuator/configuration/Storage writes.

The harness now uses the installed engine's 50,000-instruction limit for boundary tests, but does not reproduce the game's instrumentation costs. Engine inspection identified simulation-frame timing and output-buffer behavior in the installed `Sandbox.Game.dll` (SHA-256 `DE04E9162BCF87FC07CEC377213D99533BCFE1C34C9E1E2231168144C9D67EDF`). No game process or save was modified during development.

Actual physics, live instruction cost and the user's pivot measurements remain unverified. The existing v2.1 control policy, supported topology boundaries and documented differences from the fixed HeadOnly alternative otherwise remain in force. Volumetric collision restrictions are still future work.

For development, edit the fragments in `src/`, including the new `AutoArm.Pivot.cs.txt`, then run `tools/Build-AutoArm.ps1 -Test`.
