# AutoArm topology/control seam

All C# fragments are pasted inside `Program` and target the programmable-block C# 6 API.

```csharp
var topology = new AutoTopology(this, ArmName);
string why;
if (!topology.Discover(out why)) { /* remain Off; Echo why */ }
// On later stopped Main ticks; do not call Discover again while settling:
if (!topology.StableSetup(out why)) { /* wait or report */ }
```

`AutoTopology` exposes `Groups`, `Base`, `Head`, `Fingerprint`, `Report`, and `Ready`. A successful `Discover` zeros only actuators in the accepted groups and leaves `Ready=false`. `StableSetup` requires two unchanged scans with zero owned targets, captures member `Q0`, then sets `Ready=true`; the caller must supply distinct positive-time observations. `Validate(out why, bool full = false)` checks cached known connections and synchronization every call, performing a full snapshot every 30 active calls or when `full` is true. Failure clears `Ready` and zeros owned actuators. Call default validation once before each active control pass and force validation for explicit safety/setup boundaries. Unknown additions/markers have up to 30 active-pass detection latency.

Each ordered `AutoGroup` exposes `Index`, `Rotary`, exact sorted-ID `Sig`, `Label`, `MoveW`, `TurnW`, members `M`, and the last refreshed full physical `Lin`/`Ang` twist. Each `AutoMember` exposes block `B`, command `Sign`, setup position `Q0`, endpoints `U`/`V`, and `Home` storage. Rotary native units are radians and radians/second; piston native units are metres and metres/second.

Before solving each group, call:

```csharp
double lo, hi;
if (!topology.Refresh(group, topology.Head.GetPosition(), baseFrame,
                      out lo, out hi, out why)) { /* already stopped */ }
```

`Refresh` verifies live member screws and pair synchronization, writes the real full twist to `group.Lin/Ang`, and returns the common signed physical/travel velocity interval. The controller intersects that interval with its cap and acceleration bounds. Weights are allocation preferences and must not mask components of the physical twist. Commit a generalized rate using `topology.Set(group, qdot)`; call `ZeroOwned()` for explicit Stop.

Configuration uses:

```csharp
bool AutoConfig.LoadOrGenerate(Program owner, AutoTopology topology, string customData,
    out string updated, out bool changed, out string why)
```

It generates Format 5 `[AutoArm]`, `[Config]` and `[Tools]` sections; Instructions is removed. Nonblank Custom Data must have the exact current integer format and matching arm name; otherwise it is rejected with instructions to delete Custom Data. There is no config migration. The readable `Actuators` table contains ordinal/name, kind and two preferences (0..10); physical signatures are stored in `[AutoArm Config]` in PB Storage, scoped to the PB entity and arm name. Existing rows are joined to current groups only by exact signatures. A blank table explicitly resets preferences; missing/corrupt identity metadata for a populated table refuses safely. Unrelated INI keys/sections remain, and opaque Storage survives in `MyIni.EndContent`. All settings, weights and text are validated/generated before the CustomData setter is committed, followed by Storage and live settings. Invalid input leaves both documents unchanged, apart from stopping motion/readiness. Blank data may generate an editable template before successful discovery. `Reload` remains Off until the normal setup sequence and an explicit On. Toolbar arguments are named commands; numeric aliases retain their fixed meanings.

Velocity/acceleration caps are now instance settings; topology travel braking reads the same configured acceleration as control and Home. Saved homes use `[AutoArm Home]` and preserve the identity cache and unrelated Storage. Matching older Home sections migrate without hardcoded product names.

Hold learns a bounded linear/angular load bias and retains it in the deadband. Allocation anti-windup compares bound-limited delivery with an unconstrained damped reference so regularization does not suppress learning. Reference calculation is skipped when no bounds are active; there the constraint residual is exactly zero. Both live translation and roll limit opposing correction to half the pilot demand.

`MovementMode` is a validated `PilotMode` (`HEAD`, `HRZ`, `VRT`) persisted in Config; `Mode`, `MovementMode` and `ControlMode` commands change it temporarily without recapturing targets. HEAD translation uses live Head Forward/Left/Up. HRZ uses active cockpit Forward/Left/Up; VRT uses cockpit Up/Left/Backward. Active controllers may be anywhere on the same mechanical construct. Cached controllers must still be active and in that construct; handover scans are bounded to ten inactive polls and direction commands force a query. No inactive frame or Base fallback is used for HRZ/VRT direction commands.

View vectors are converted into the Base task frame and orthonormalized before use. Mouse yaw is negative view Up, pitch is view Left; VRT mouse uses the raw cockpit view axes, independently of its remapped translation axes. Roll always uses actual Head Forward. Combined angular pilot demand is capped by HeadTurnSpeed. Pure keyboard translation never rotates/re-normalizes the held orientation target. HRZ/VRT Face and yaw/pitch presets use raw cockpit cardinal axes; HEAD presets retain the prior Base/head conventions. GoHome takeover tests enabled raw input, so Speed=0 or canceling angular contributions cannot mask operator input.

Supported split/rejoin sections have equal path length, internally distinct grids, the same actuator kind at each corresponding ordinal, and matching full head-point screws after sign. This operationally accepts symmetric 1:1 copies such as piston–coaxial rotor–piston. Exact fixed space screws, equal initial end transforms, and common coordinates would give identical serial products; this script measures instantaneous head-point twists with finite tolerances instead, so it does not prove that theorem for an arbitrary build. It rechecks before commands and stops on divergence. Unequal paths, non-coaxial rotary copies, variable ratios, cross-links, and general closed linkages are rejected. Components touching one corridor grid are reported and ignored; they are never commanded. Components reconnecting to two corridor grids are rejected.

Tool swaps use `ToolSwapController`, `ToolRegionScan`, `ToolTopInfo` and `ToolGeometry`. Each ToolNN.Mount names a tool-side rotor base. The arm-side Mount name and manual ArmTip fields are removed. BuildArm maps moving grids from the named attached Base, excluding its hull and configured tool couplers. An existing attached tool is accepted only when its reciprocal rotor part is in that moving component. Otherwise bounded occupied-cell scans cover all reachable grids; known attached tops are masked. One hidden 1-cell or 3x2x3 region with one plausible mounting face supplies a pivot and signed shaft axis. Multiple parts, sparse/unsupported shapes or ambiguous faces refuse.

Topology.Head remains the terminal identity marker; End is the live grid anchor and EndPose/EndPosition supply the actual focus. Bare focus is the inferred rotor part; mounted focus is the configured tool marker. This includes an otherwise unnamed final hinge in the route and stop ownership. Overrides match marker ID plus captured name, allowing duplicate generic joint names.

Unknown rotor azimuth is not guessed as identity. First acquisition aligns native pivot/axis while preserving an arbitrary continuous arm roll; it requires unlimited target-rotor limits, journals and temporarily releases a requested Rotor Lock, then adopts only an actual supported part matching cell, finite shaft axis, arm grid and reciprocal owner. Its observed rigid frame replaces the provisional frame. Two live pose/support observations gate lock restoration and support release. Relock intent survives interrupted attachment; explicit scans restore it only for a verified aligned attachment or a confirmed bare, non-pending base. No blind candidate retries or per-tool teaching are used. Coupler geometry Storage is unnecessary: mounted capture or automatic bare detection runs again at startup.

Mechanical upstream identity is guarded independently of the temporarily excluded detachable mount. `ToolPoseStep` uses the existing solver, physical bounds and output commit against live marker-relative goals. All selected support ports must be locked before detachment; actual attached-top identity, reciprocal Base and a newly recomputed live coupling pose gate support release. Cached pose agreement cannot substitute for those checks. Actual grid separation precedes endpoint rebuild. Expected swap boundaries preserve weights by physical signed-member identities while rewriting endpoint/grid signatures.

Operations journal mechanical mutations before writes. Cancellation stops without rollback; passive discovery cannot clear recovery. Explicit successful `ToolScan`, or the explicit scan requested by `On`, reconciles it. `On` reloads configuration and rediscovers before requesting activation; tool scans keep a deferred activation flag which cancellation/load consumes or clears. Successful mounting (including selection of the already mounted tool) invokes the existing `EnableControl` boundary after settlement. That boundary rechecks manual permission and topology, captures the current pose, resets old demands and waits for a positive timed update before motion. Parking and passive/diagnostic scans stay Off. Both manual control and every Home pass call `AllowManual` so an unexpected attachment to the excluded mount cannot escape validation. Startup `ZeroSavedDrives` stops same-construct actuators from a PB/arm-scoped identity cache and a uniquely named configured mount before configuration errors can prevent ordinary discovery from acquiring ownership. It does not switch connections or command unrelated drives.
