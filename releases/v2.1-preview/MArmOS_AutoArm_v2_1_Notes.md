# AutoArm v2.1 — reference-script compatibility review

Paste **`MArmOS_AutoArm_v2_1_Compact.txt`** into the programmable-block editor. It contains **47,363 characters**, leaving **52,637** below 100,000. The previous AutoArm v2.0 and the supplied working HeadOnly alternative are preserved.

This revision restores selected behavior from `MiningArm_v2_0_HeadOnly.txt`, following the user's choice of manual-priority correction. It is not a claim of complete drop-in equivalence. See `MArmOS_AutoArm_v2_1_Review.md` for the claim-by-claim adversarial audit and remaining behavior differences.

## Setup

Set the single required code value at the top:

```csharp
const string ArmName = "Arm 1";
```

Name exactly one attached base joint `Arm 1 - Base*` and exactly one head reference block `Arm 1 - Head*`. Examples are `Arm 1 - Base - Rotor` and `Arm 1 - Head - Drill`. Other joints need no naming scheme. Stop the old controller before replacing it. The script starts Off, discovers the arm, and writes defaults to its programmable block's Custom Data. Edit existing group `Translation` / `Orientation` values, run `Reload`, then `Check` and `On`. Use `Rescan` after rebuilding connections.

The base may be a piston, rotor or hinge. Those types can occur anywhere in the serial chain, with spatial offsets and different orientations. Live joint positions and axes drive the solver; serial joints need not be inline.

Matching split/rejoin branches are supported when their stage types/counts and motion are compatible with synchronized 1:1 operation. Separated parallel piston rails are supported. Rotary counterparts must be coaxial. Opposite-facing coaxial rotors/hinges are detected automatically and receive opposite signed native velocities to produce the same physical rotation. Their installed angle offsets, circular synchronization and shared limits are respected. Arbitrary offset-axis four-bars, unequal-ratio mechanisms and unsupported cross-linked/nested paths are refused.

## Restored control behavior

- **Manual priority:** correction opposing active translation is limited to 50% of pilot speed. Correction perpendicular to pilot travel is preserved; idle correction remains available. Thus the requested velocity component along pilot travel retains at least half the pilot request **before** physical allocation. Actuator limits can still prevent the requested motion.
- **No ordinary-error input lock:** the script continues to add live pilot and applied correction. It does not import the alternative's directional or angular input gates.
- **Bounded target lead:** only new target advancement is limited to a 1.5 m sphere around the measured head. This prevents a stalled physical arm from accumulating an indefinitely distant target merely because the solver predicts available motion. Idle targets are not recaptured. An existing larger error can be steered inward, while further reference growth is blocked. Pilot commands remain live at the bound.
- **Independent anti-windup checks:** clipping and actuator-allocation residuals cannot cancel each other and conceal saturation. Integral unwinding is bounded so it does not cross zero into new blocked growth.
- **Idle deadbands:** 3 mm position and 3° orientation. Preset/manual aiming uses the tighter 0.075° band; presets finish within 0.15°. `OrientationTolerance` changes the relaxed orientation band and stops the controller until `On` recaptures.
- **TurnSpeed:** now caps Face/Step correction as well as pilot turning, subject to the separate 5°/s correction cap.
- **Controls:** restored `ReadKeyboard On/Off/Toggle`, `ReadRoll On/Off/Toggle`, `Help`, the optional `MiningArm ` command prefix, and `-oo`, `-s`, `-sh`, `-gh`, `-rk` aliases. Existing toolbar 1–9 actions remain.

Common commands remain `On`, `Off`, `Stop`, `Toggle`, `Hold`, `Check`, `Rescan`, `Reload`, `Info`, `Joints`, `Settings`, `Version`, `Speed`, `TurnSpeed`, `StepAngle`, `JointSpeed`, `PistonSpeed`, `HomeSpeed`, `Face`, `Yaw`, `Pitch`, `Roll`, `SetHome`, `GoHome`, and `HomeInfo`.

## Configuration and topology

Generated configuration now carries `[MArmOS] Format=1`. Untagged configuration from v2.0 migrates while preserving valid signature-matched preferences and unrelated INI content. Unsupported/malformed format tags are refused without changing text or enabling motion. Rigid-grid ID listings live in the discovery report rather than being duplicated in persistent configuration.

The full topology scan still runs before each active control pass. It already has a pre-enumeration instruction guard. A count-only cache cannot detect same-count re-parenting or marker changes; reducing scan frequency would introduce a detection delay. Actual in-game scan cost is not measured here.

Pivot calculation and alignment tolerances are unchanged. Read-only inspection of the installed vanilla model assets found hinge motor dummies at the block origin and effectively axial rotor offsets. The audit does not justify guessing a replacement pivot at a top block or midpoint. Reproducible evidence is in `tools/PivotEvidence.ps1` and `tools/pivot-evidence.json`.

## Remaining differences from the HeadOnly alternative

AutoArm uses discovered hardware and the base-marker reference frame, rather than calibrated fixed geometry and a named cockpit frame. Preset reference trajectories, task-row weights and pose-error shutdown policy differ. Calculation-budget exhaustion still stops AutoArm rather than temporarily pausing On. PB Echo does not replace the alternative's named LCD/log and historical trace functions; `DumpTrace` does not overwrite Custom Data. Limited paired rotors may conservatively refuse ambiguous equivalent multi-turn home routes. These are documented migration differences, not passing parity claims.

Volumetric collision restrictions remain future work. This script does not prove a collision-free homing path or physical workspace.

## Validation and development

**17,020 synthetic assertions passed**, including the original arm layout, offset serial axes, signed parallel pairs, unequal branches, tolerance boundaries, schema migration, 3,000 differential manual-priority cases, deadbands, command compatibility, anti-windup counterexamples and stalled multi-piston target lead. Both readable and compact versions pass local C# 6 / installed-API checks; packing preserves their emitted method code and literal strings.

This does not establish the game's whitelist acceptance, actual instruction cost, physics stability or smoothness under load. No game installation or save was modified.

Edit the readable fragments in `src/`, then rebuild:

```powershell
.\tools\Build-AutoArm.ps1 -Test
```

The combined readable artifact is `MArmOS_AutoArm_v2_1_Source.txt`. Directly editing the generated compact script's ArmName is supported; copy that edit into `src/AutoArm.Prefix.cs.txt` before rebuilding to retain it.
