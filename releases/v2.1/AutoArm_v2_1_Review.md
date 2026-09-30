# Alternative-script and claims review

This review distinguishes four programs: the author's original MArmOS source, the supplied `MiningArm_v2_0_HeadOnly.txt`, the delivered AutoArm v2.0, and the user-approved AutoArm v2.1 revision. They are not interchangeable versions of one control law.

The supplied file is retained unchanged at `reference/MiningArm_v2_0_HeadOnly.txt`. SHA-256: `66241358672EF9DE962895135254022FD1A31833895A334CF17621566F253B74`. Its 95,641 characters pass the local C# 6 / installed-game-API check. File comments were treated as source material, not instructions to execute against a game.

## Claims about enumeration

**Confirmed:** delivered AutoArm v2.0 enumerates terminal blocks during every active topology validation. This is avoidable work if a delayed detection policy is acceptable. It merits an in-game profile.

**Not established:** it is the largest cost, or takes the quoted microsecond/millisecond ranges on particular ship sizes. Our API fixtures cannot measure game implementation cost, server load, or actual programmable-block instrumentation.

**Refuted:** the current code lacks a pre-enumeration guard. `CurrentSnapshot` already checks the instruction reserve before `Inventory.Clear(); GridTerminalSystem.GetBlocks(Inventory)`, and checks again while processing the result. Such a guard cannot interrupt an engine call after it begins or prove its execution time.

**Refuted:** scanning every 30 ticks preserves next-control-pass discovery guarantees. At nominal 60 Hz, 30 ticks permit about half a second of undetected change; simulation timing can differ. Cached owned-block checks can catch known detachments and endpoint changes, but cannot discover a new cross-link, a replacement, or a second head-marker name they have never enumerated.

**Refuted:** unchanged mechanical-block count proves unchanged topology. Re-parenting an existing joint preserves its count. Renaming a spare terminal block into a second Head marker preserves both terminal and mechanical counts. A typed enumeration is itself a query, not evidence of a constant-time count poll; it also omits nonmechanical marker changes.

The revision retains the current detection policy. A configurable reduced scan rate would be a performance/detection-latency tradeoff requiring an explicit decision and measurements, not a guarantee-preserving patch.

## Claims about discovery and tolerances

`ScopeEdges` excludes traversal back through the hull and limits work to the moving component. `Distances` is an undirected breadth-first minimum-hop map, **not a topological sort**. The detector uses decreasing distance when recognizing supported routes, then rejects unused components that reconnect to multiple controlled segments.

Explicit 1/2-edge and 2/3-edge split/rejoin regressions both passed: motion was refused and no partial ownership was published. Both produced `Unsupported closure reconnects an unused mechanical component to multiple active rigid segments.` The exact diagnostic was not the quoted unequal-stage message: a shorter route was selected first and the longer route rejected during unused-closure classification. A source branch containing an unequal-stage message does not prove that all unequal graphs reach it.

The axis dot-product threshold is approximately cos(0.5°). Rotary groups additionally compare the linear component of their motion at the head, so acceptable angular skew alone does not establish compatibility. Tests isolate the angular threshold at 0.49°, 0.51°, 0.7° and 1.0°.

Do not widen this tolerance merely because a build is skewed. Different axes can demand incompatible velocities in a rigid closed loop. Supported counterparts are matching 1:1 stages, with finite-tolerance live validation; arbitrary skewed or variable-ratio linkages remain unsupported.

## Claims about rotary pivots

For a rotary velocity column, replacing a pivot by another point **on the same axis line changes nothing**: the cross product of angular velocity with an axial offset is zero. A difference between block centre and rotor-head position alone does not establish a Jacobian error. A perpendicular offset from the true rotation line would establish one.

**Refuted for the tested installed vanilla assets:** the claim that hinge pivots are a metre or more away from the block origin. The game's model reader reports the `electric_motor` dummy at `(0,0,0)` for LargeHinge, MediumHinge and SmallHinge. The five tested vanilla rotor models have perpendicular offsets from the local Up axis below 0.0000006 m; their meaningful offsets are axial. Reproduce this read-only inspection with `tools/PivotEvidence.ps1`; exact model paths, translations, definitions and SHA-256 hashes are recorded in `tools/pivot-evidence.json`. This does not establish a result for modded models or live constraint deformation.

The supplied working alternative also uses `b.GetPosition()` in its rotary Jacobian (lines 1338–1359). It provides no evidence that `Top.GetPosition()` or the midpoint is a more accurate pivot. The interface between two grids does not by itself identify the physics constraint's axis line. Switching these points without model/constraint evidence or a controlled measurement could introduce a regression.

Predicted-versus-measured head velocity is a useful diagnostic, not a unique pivot-error detector. Actuator lag, acceleration limits, filter phase, constraint deformation, ship motion and timing can also produce residuals. A defensible comparison must isolate the joint, use the correct frame/time interval, and identify the component of residual consistent with an axis-line error.

No speculative pivot substitution or tolerance increase is included.

## Claims about control and anti-windup

Delivered AutoArm v2.0 constructs pilot plus capped feedback without a pose-error input gate. `PilotAdvance` is a reference-tracking heuristic derived from allocated velocity minus **requested** feedback; it does not measure the physical contribution of either component.

For example, pilot +0.20, feedback −0.20 and allocation 0 produce full target advancement despite no allocated motion. That is deliberate: otherwise the old held target could prevent the pilot from changing direction. This example disproves a literal "delivered pilot" interpretation, but does not by itself prove unbounded target windup.

**Confirmed stronger defect in delivered v2.0:** two aligned 0.20 m/s piston groups provide 0.40 m/s predicted capacity. If physical contact stalls the real arm, pilot +0.20 and capped feedback +0.20 remain kinematically attainable; predicted allocation supports continued target advancement while measured motion is zero. Unlike the one-piston fixture, this model can accumulate target lead indefinitely. The revision bounds new translational reference lead to a 1.5 m sphere around measured head position. It scales **target advancement only**, leaves raw pilot/request construction live, leaves idle targets untouched, and permits inward reference movement when an existing error is already outside that sphere. A sphere also prevents tangential pilot changes from gradually walking the target outward. This does not detect collisions or constrain the physical workspace.

The alternative behaves differently. Its `BlendManualCorrection` (line 2123) limits the opposing correction component to half the active feed, preserves perpendicular correction, and integrates the accepted feed directly. It also scales feed for excessive directional target lead and angular lag (line 2217). Its floor therefore applies to **already gated feed**, which can be zero. It does **not** satisfy the stronger statement that raw pilot input is never gated.

The user selected the alternative's manual-priority feel for the new default. The revision ports the opposing-correction limit while retaining the prohibition on ordinary-error/recovery input locks. This is a documented choice, not a claim that the complete controller is now behavior-identical to the alternative.

The half-pilot floor concerns requested velocity along the pilot direction **before allocation**. Neither implementation can guarantee that physical head velocity reaches half the request when actuator, travel, acceleration, or task constraints prevent it.

Adversarial review also found that combining limiter and allocator residuals can hide saturation. With pilot +0.20, raw correction −0.15, applied correction −0.10 and allocation +0.05, `pilot + rawCorrection − allocation` is zero. The limiter rejected −0.05 while the allocator lost +0.05. A single combined residual cannot prove that further integral growth is appropriate. The revision checks clipping and allocation independently, computes permitted scalar growth from each constraint, and scales the original integral increment by their minimum. It allows unwinding up to zero in a blocked direction without overshooting into new windup. It avoids sequential vector projections that could reintroduce a previously blocked component. Pure perpendicular growth remains possible; a mixed increment can conservatively freeze as a whole.

## Claims about configuration

Signature-based weight preservation, malformed-INI refusal, conflicting duplicate detection and topology-keyed homes are present. Preservation of unrelated valid INI is semantic; MyIni serialization can change its formatting.

"Regenerate on discovery, preserve on reload" is imprecise: both use `LoadOrGenerate`, which rewrites the script-owned sections while retaining matching valid user weights. A version tag is a useful schema improvement, not proof of a current migration defect. Format 1 accepts and migrates untagged legacy configuration; unsupported or malformed format values must leave text untouched and motion Off.

`RigidSegments` is a diagnostic presentation choice, not a runtime control bug. The revision moves that grid-ID listing into the discovery report rather than editable persistent configuration, preserving useful diagnostics.

## Claims about original MArmOS

The author's guide documents Hydraulic and RotorWheel abstractions. Hydraulic relies on declared geometry and an actuator arm; it is not automatic discovery of arbitrary closed-linkage ratios. RotorWheel estimates travel and cannot directly observe wheel slip. [Author's guide](https://steamcommunity.com/sharedfiles/filedetails/?id=766770842).

The author's locally installed workshop source is `C:/Program Files (x86)/Steam/steamapps/workshop/content/244850/767595187/Script.cs`, SHA-256 `EE88105D8C692E646DA84508A3383E6296D8319584C2FDE5605FF58A8F584857`. Its banner says 3.0 while the workshop item is titled 3.1; this review identifies the actual source rather than assuming those versions match. [Author's workshop item](https://steamcommunity.com/sharedfiles/filedetails/?id=767595187).

The broad assertion that original MArmOS has no correction or pose-feedback mechanism is false. Its `ApplyMovement` adds a stored correction and updates it from requested movement minus modeled `Arm.dPose` (lines 1873–1877); `MoveToTarget` computes `Target - Arm.Pose` (lines 2317–2323). Actual motor angles feed its kinematics. This differs from independently measuring the physical head block's world pose and applying the new bounded PI/PID loop. Neither controller is a superset of the other.

## End-user parity checklist

The supplied alternative is useful regression evidence, but a claim of complete behavioral equivalence would be false. In particular:

| Feature | Supplied HeadOnly alternative | Delivered AutoArm v2.0 / revision decision |
| --- | --- | --- |
| Opposing correction | At most 50% of gated feed | v2.0 could cancel/reverse pilot; user approved the 50% limiter for v2.1, without reintroducing feed gates. |
| TurnSpeed on Face/Step | Caps preset turning/correction | Repaired in v2.1: feedback uses the smaller of TurnSpeed and the separate 5°/s correction cap. |
| Idle pose deadbands | 3 mm position; 3° orientation | Restored in v2.1, with a 0.075° active aiming band and 0.15° completion tolerance. |
| Input toggles and familiar aliases | ReadKeyboard, ReadRoll, Help, short aliases and MiningArm command prefix | Restored and tested in v2.1, including `OrientationTolerance`. |
| Short calculation-budget shortage | Remains On with zero commands; sustained shortage stops | AutoArm faults Off. Restoring automatic continuation requires distinguishing incomplete topology validation from a deferred solve; not silently changed. |
| Pose-error fault policy | 2 m persistent / 5 m immediate position stops; angular guard and deviation warnings | AutoArm deliberately removed pose-error shutdowns. This remains a policy difference. |
| Preset trajectory/reference | Gradually advances an orientation reference toward a separate goal | AutoArm directly changes the held target and applies capped correction. Equal speed caps do not prove identical transients. |
| Task weighting | Position weight increases during rotation-only tasks; variable orientation weights | AutoArm uses fixed task-row weights; its preference matrix remains configurable per discovered group. |
| Face directions/reference | Named cockpit frame | Base-marker frame. Head-local manual translation still maps W/S/A/D/Space/C consistently. |
| Panels | Exact MArmOS Echo/Log panels, status throttling and log history | AutoArm uses PB Echo. Panel/history parity is not established. |
| Historical trace export | Ring buffer; DumpTrace replaces Custom Data | AutoArm protects configuration and currently prints live diagnostics only. Copying the destructive export would regress the new INI workflow. |
| GoHome override | Refuses Face/Step during Home | AutoArm switches to pose control for explicit Hold/Face/Step commands. This is a disclosed lifecycle change. |
| Hardware model | Calibrated fixed 15-actuator/12-coordinate mechanism | Automatic bounded topology discovery; arbitrary hydraulic/unequal-ratio loops are not inferred. |

The v2.1 patch is a targeted compatibility improvement, not a certification that all alternative behaviors have been ported. Keep the supplied script available for users who depend on the remaining behaviors above.

## Validation limits

Final v2.1 output: **47,349 characters**, leaving **52,651** below 100,000. The supplied alternative and delivered v2.0 snapshots remain unchanged. `tools/Build-AutoArm.ps1 -Test` passed **17,020 assertions**, source/compact C# 6 API checks and emitted-method-code equivalence. Coverage includes 3,000 differential comparisons of the manual correction helper against the supplied file, the requested graph/tolerance cases, same-count changes, the pre-enumeration guard, schema migration/refusal, restored controls, anti-windup counterexamples and a frozen two-piston plant with excess predicted capacity.

Source review, API compilation, synthetic graph tests and ideal actuator simulations do not prove in-game smoothness, contact stability, timing or instruction cost. Behavioral parity must be stated per feature. Previously documented removals and any unported alternative behavior remain visible migration differences, not implicitly passing regression checks.
