# AutoArm topology/control seam

The generated scripts target the programmable-block C# 6 API. The host lives in Program; reusable engine fragments are assembled once inside ArmCore and instantiated per arm. The examples below describe the engine boundary, not an instruction to paste fragments individually.

```csharp
var topology = new AutoTopology(this, ArmName);
string why;
if (!topology.Discover(out why)) { /* remain Off; Echo why */ }
// On later stopped Main ticks; do not call Discover again while settling:
if (!topology.StableSetup(out why)) { /* wait or report */ }
```

`AutoTopology` exposes `Groups`, `Base`, `Head`, `Fingerprint`, `Report`, and `Ready`. A successful `Discover` zeros only actuators in the accepted groups and leaves `Ready=false`. `StableSetup` requires two unchanged scans with zero owned targets, captures member `Q0`, then sets `Ready=true`; the caller must supply distinct positive-time observations. `Validate(out why, bool full = false, bool refreshBeforeCommit = false)` checks markers, endpoint and cached connections, performing a full snapshot every 30 active calls or when `full` is true. Default/full validation includes all edges and synchronization. Active control/Home pass `refreshBeforeCommit=true`: validation checks only a precomputed list of unowned edges, and each owned group's mandatory `Refresh` covers attachment, unchanged exact endpoints, names, bounds and parallel synchronization before any output commit. Callers must refresh every group before committing when using this option. Failure clears `Ready` and zeros owned actuators. Unknown additions/markers retain up to 30 active-pass detection latency; owned attachment/sync failures remain immediate.

Each ordered `AutoGroup` exposes `Index`, `Rotary`, exact sorted-ID `Sig`, `Label`, `MoveW`, `TurnW`, members `M`, and the last refreshed full physical `Lin`/`Ang` twist. Each `AutoMember` exposes block `B`, command `Sign`, setup position `Q0`, endpoints `U`/`V`, and `Home` storage. Rotary native units are radians and radians/second; piston native units are metres and metres/second.

Before solving each group, call:

```csharp
double lo, hi;
if (!topology.Refresh(group, topology.Head.GetPosition(), baseFrame,
                      out lo, out hi, out why)) { /* already stopped */ }
```

`Refresh` verifies live member screws and pair synchronization, writes the real full twist to `group.Lin/Ang`, and returns the common signed physical/travel velocity interval. The controller intersects that interval with its cap and acceleration bounds. Weights are allocation preferences and must not mask components of the physical twist. Commit a generalized rate using `topology.Set(group, qdot)`; call `ZeroOwned()` for explicit Stop.

`PruneToolWeights` runs after successful stopped discovery/setup, including cold load. It removes a saved signed-member key if any actuator identity is absent, closed or has the wrong actuator family. Live parked/nonfunctional actuators are retained regardless of changed grid identity. No terminal enumeration is added to active ticks. Cleanup collects removals before committing; an exhausted instruction reserve or failed discovery leaves persisted weights unchanged. Other Storage sections are preserved.

Configuration uses:

```csharp
bool AutoConfig.LoadOrGenerate(Program owner, AutoTopology topology, string customData,
    out string updated, out bool changed, out string why)
```

The public host accepts [global] Format 7 (Arm) or Format 2 (ToolSwap), plus named arm sections with global inheritance and local overrides. Old external formats are rejected with instructions to delete Custom Data. Stage-based shared preferences check joint kinds/counts and parallel layout; per-instance physical identity maps, Home, tool weights and journals are stored in separate State <arm> blobs scoped to the PB and arm. Each hosted core receives a direct effective MyIni configuration view. One shared Stage/Kind parser handles public body templates; signed physical preferences and scoped identity rows remain in Storage. Private serialization/format validation exists only in marked standalone test adapters stripped from hosted assembly. Unknown/malformed targets refuse before dispatch; runtime/setup faults remain scoped to the affected arm except explicit host/aggregate-budget failures. See the multi-arm host contract in docs/MODULES.md.

Velocity/acceleration caps are now instance settings; topology travel braking reads the same configured acceleration as control and Home. Saved homes use `[AutoArm Home]` and preserve the identity cache and unrelated Storage. Matching older Home sections migrate without hardcoded product names.

Hold learns a bounded linear/angular load bias and retains it in the deadband. Allocation anti-windup compares bound-limited delivery with an unconstrained damped reference so regularization does not suppress learning. Reference calculation is skipped when no bounds are active; there the constraint residual is exactly zero. Both live translation and roll limit opposing correction to half the pilot demand.

`MovementMode` is a validated `PilotMode` (`HEAD`, `HRZ`, `VRT`) persisted in Config; `Mode`, `MovementMode` and `ControlMode` commands change it temporarily without recapturing targets. HEAD translation uses live Head Forward/Left/Up. HRZ uses active cockpit Forward/Left/Up; VRT uses cockpit Up/Left/Backward. Active controllers may be anywhere on the same mechanical construct. Cached controllers must still be active and in that construct; handover scans are bounded to ten inactive polls and direction commands force a query. No inactive frame or Base fallback is used for HRZ/VRT direction commands.

View vectors are converted into the Base task frame and orthonormalized before use. Mouse yaw is negative view Up, pitch is view Left; VRT mouse uses the raw cockpit view axes, independently of its remapped translation axes. Roll always uses actual Head Forward. Combined angular pilot demand is capped by HeadTurnSpeed. Pure keyboard translation never rotates/re-normalizes the held orientation target. HRZ/VRT Face and yaw/pitch presets use raw cockpit cardinal axes; HEAD presets retain the prior Base/head conventions. GoHome takeover tests enabled raw input, so Speed=0 or canceling angular contributions cannot mask operator input.

Supported split/rejoin sections have equal path length, internally distinct grids, the same actuator kind at each corresponding ordinal, and matching full head-point screws after sign. This operationally accepts symmetric 1:1 copies such as piston–coaxial rotor–piston. Exact fixed space screws, equal initial end transforms, and common coordinates would give identical serial products; this script measures instantaneous head-point twists with finite tolerances instead, so it does not prove that theorem for an arbitrary build. It rechecks before commands and stops on divergence. Unequal paths, non-coaxial rotary copies, variable ratios, cross-links, and general closed linkages are rejected. Components touching one corridor grid are reported and ignored; they are never commanded. Components reconnecting to two corridor grids are rejected.

The separate ToolSwap PB uses `ToolSwapController`, `ToolRegionScan`, `ToolTopInfo` and `ToolGeometry`. The arm PB uses `ToolSwapLink` to exchange requests and authoritative state, and a pose-only `ArmTipInfo` descriptor for received focus frames; it contains no parking/attachment state machine. See [the module contract](../docs/MODULES.md). Automatic profiles name only one terminal marker per numbered head. The rigid region determines its rotor base and facing support merges; verified hardware and parked pose are persisted on the marker. Explicit ToolNN profiles remain an optional advanced path. The arm-side Mount name and manual ArmTip fields are removed. BuildArm maps moving grids from the named attached Base, excluding its hull and configured tool couplers. An existing attached tool is accepted only when its reciprocal rotor part is in that moving component. Otherwise bounded occupied-cell scans cover all reachable grids; known attached tops are masked. One hidden 1-cell or 3x2x3 region with one plausible mounting face supplies a pivot and signed shaft axis. Multiple parts, sparse/unsupported shapes or ambiguous faces refuse.

Topology.Head remains the terminal identity marker; End is the live grid anchor and EndPose/EndPosition supply the actual focus. Bare focus is the inferred rotor part; mounted focus is the configured tool marker. This includes an otherwise unnamed final hinge in the route and stop ownership. Overrides match marker ID plus captured name, allowing duplicate generic joint names.

Unknown rotor azimuth is not guessed as identity. First acquisition aligns native pivot/axis while preserving an arbitrary continuous arm roll; it requires unlimited target-rotor limits, journals and temporarily releases a requested Rotor Lock, then adopts only an actual supported part matching cell, finite shaft axis, arm grid and reciprocal owner. Its observed rigid frame replaces the provisional frame. Two live pose/support observations gate lock restoration and support release. Relock intent survives interrupted attachment; explicit scans restore it only for a verified aligned attachment or a confirmed bare, non-pending base. No blind candidate retries or per-tool teaching are used. Coupler geometry Storage is unnecessary: mounted capture or automatic bare detection runs again at startup.

Mechanical upstream identity is guarded independently of the temporarily excluded detachable mount. `ToolPoseStep` uses the existing solver, physical bounds and output commit against live marker-relative goals. All selected support ports must be locked before detachment; actual attached-top identity, reciprocal Base and a newly recomputed live coupling pose gate support release. Cached pose agreement cannot substitute for those checks. Actual grid separation precedes endpoint rebuild. Expected swap boundaries preserve weights by physical signed-member identities while rewriting endpoint/grid signatures.

Operations journal mechanical mutations before writes. Cancellation stops without rollback; passive discovery cannot clear recovery. Explicit successful ToolScan or On reconciles it. AutoArm executes complete ArmPath lists, including live-anchor transforms, tolerances, measured settling and progress. ToolSwap waits for matching completion and stopped-output acknowledgement before changing attachments. Its CancelMotion issues Stop without blocking local cancellation; hardware mutation paths use the acknowledged StopMotion fence. Native pending attachment is cleared only while confirmed bare. Successful mounting invokes the arm's EnableControl boundary after settlement; parking and diagnostic scans stay Off. Module timeouts revoke movement, and session epochs/generations fence restart and queued commands. Both manual control and Home passes retain live equipment permission checks. Scoped saved drive identities are zeroed on startup before failed discovery can leave native velocities active.
