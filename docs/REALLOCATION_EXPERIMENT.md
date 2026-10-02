# Automatic-path reallocation experiment

The current Safety filter uniformly scales the velocity chosen by the arm solver. One closing joint can therefore throttle the whole arm even when another joint combination could move the head safely. This experiment attempts a bounded alternative for automatic paths before accepting that limitation. It does not increase timeouts or relax collision/return tolerances.

The ready-to-paste [Arm prototype](../AutoArm_Reallocation_Prototype_Compact.txt) replaces Arm PB code only. Keep Custom Data and the current v5.0.4 Collision PB, ToolSwap and settings. It is experimental: the captured small Smoke still passes, but the larger captured Run still does not complete. No fix for the reported live return timeout is claimed until it is tested at that arm posture. The production Arm script remains unchanged and is the rollback file.

## Try it

1. Cancel the active Bench path before replacing code. Copy its completed/aborted report first.
2. Paste the complete prototype into Arm PB, retaining Custom Data. Run `On(Arm 1)` and `On(Arm 2)`; keep Collision PB running.
3. Replace only Bench code with the current [AutoArm_Bench.txt](../AutoArm_Bench.txt), keeping its Custom Data. Run `Rebase`, `Check`, then `Smoke` from the settled current posture. Leave cockpit input neutral.
4. Bench now shows candidate command scale, row count/reason and measured net head speed. `REALLOCATED` means an alternative passed the checks. ArmService exposes `SafetyReallocated`, `SafetyRepairPasses` and `SafetyRepairBudget`.
5. Send the complete report after success/abort, using `Page N` when clipboard text truncates. For an exact replay of a changed posture, the geometry survey also needs the current joint states; head poses alone do not determine redundant joint posture.

To revert, paste [AutoArm_Compact.txt](../AutoArm_Compact.txt), keep Custom Data and run On for both arms. Clearance, guidance freshness, packet/row capacity, physical limits and normal output commit remain enforced throughout.

## Method and limitations

Only automatic pose/path motion participates. Ordinary cockpit control and Home retain the original filter. The original scaled candidate is preserved as fallback. The experiment uses a bounded dual-coordinate projection in the existing normalized joint coordinates, including every received collision inequality and every joint/acceleration interval. Its metric uses the current weighted Cartesian Jacobian and damping; the Woodbury identity reuses the existing six-dimensional factorization instead of a general dense joint-space inversion.

At most twelve sweeps run, with a 65% instruction-work guard. A candidate must be finite, inside the current command intervals, satisfy every received row, and improve the weighted task objective compared with the original filtered output. The existing strict uniform row veto runs again before output; if it invalidates that advantage, the original result is restored. Uncertain, infeasible, stale, nonconvergent or exhausted cases preserve the original conservative output. Collision geometry and possible false positives are not changed. This is local allocation, not global planning, and it may still stall.

Dual coordinate projection is a standard approach to inequality-constrained quadratic problems; the bounded implementation here makes no claim of convergence in twelve sweeps. [Hildreth row-action method](https://epubs.siam.org/doi/10.1137/0318033)

## Validation

- Both artifacts pass installed SE C#6/type-safety/memory-safe compilation and packing token/IL checks. Compact size: **96,067 characters**, 3,933 free.
- **327 focused assertions per artifact** cover redistribution to a redundant safe piston, 160 randomized contact ceilings with retained head motion, all-row/joint bounds, opposed constraints, manual control, stale guidance and budget fallback.
- Existing collision source regression suite: **703 assertions** against the prototype.
- The exact compact Arm native ResourceMonitoringRewriter/IlInjector replay completes both Smoke paths. Over 4,358 Arm invocations the peak is **18,937/50,000 instructions**, median 8,282. Eight sampled states report reallocation; peak search twelve sweeps, zero sampled repair-budget flags. Proxy kinematics/API costs do not establish real flex/contact physics or wall-clock performance.
- The larger captured Run still aborts. The live 17 mm return case is not reproduced exactly by the older survey; its current joint-state geometry is missing. This experiment is available for comparison, not presented as a verified resolution of that timeout.
