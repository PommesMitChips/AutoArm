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

It generates Format 3 `[AutoArm]`, `[Instructions]`, `[Config]` and `[Tools]` sections. Nonblank Custom Data must have the exact current integer format and matching arm name; otherwise it is rejected with instructions to delete Custom Data. There is no config migration. The readable `Actuators` table contains ordinal/name, kind and two preferences (0..10); physical signatures are stored in `[AutoArm Config]` in PB Storage, scoped to the PB entity and arm name. Existing rows are joined to current groups only by exact signatures. A blank table explicitly resets preferences; missing/corrupt identity metadata for a populated table refuses safely. Unrelated INI keys/sections remain, and opaque Storage survives in `MyIni.EndContent`. All settings, weights and text are validated/generated before the CustomData setter is committed, followed by Storage and live settings. Invalid input leaves both documents unchanged, apart from stopping motion/readiness. Blank data may generate an editable template before successful discovery. `Reload` remains Off until the normal setup sequence and an explicit On. Toolbar arguments are named commands; numeric aliases retain their fixed meanings.

Velocity/acceleration caps are now instance settings; topology travel braking reads the same configured acceleration as control and Home. Saved homes use `[AutoArm Home]` and preserve the identity cache and unrelated Storage. Matching older Home sections migrate without hardcoded product names.

Hold learns a bounded linear/angular load bias and retains it in the deadband. Allocation anti-windup compares bound-limited delivery with an unconstrained damped reference so regularization does not suppress learning. Reference calculation is skipped when no bounds are active; there the constraint residual is exactly zero. Both live translation and roll limit opposing correction to half the pilot demand.

`MovementMode` is a validated `PilotMode` (`HEAD`, `HRZ`, `VRT`) persisted in Config; `Mode`, `MovementMode` and `ControlMode` commands change it temporarily without recapturing targets. HEAD translation uses live Head Forward/Left/Up. HRZ uses active cockpit Forward/Left/Up; VRT uses cockpit Up/Left/Backward. Active controllers may be anywhere on the same mechanical construct. Cached controllers must still be active and in that construct; handover scans are bounded to ten inactive polls and direction commands force a query. No inactive frame or Base fallback is used for HRZ/VRT direction commands.

View vectors are converted into the Base task frame and orthonormalized before use. Mouse yaw is negative view Up, pitch is view Left; VRT mouse uses the raw cockpit view axes, independently of its remapped translation axes. Roll always uses actual Head Forward. Combined angular pilot demand is capped by HeadTurnSpeed. Pure keyboard translation never rotates/re-normalizes the held orientation target. HRZ/VRT Face and yaw/pitch presets use raw cockpit cardinal axes; HEAD presets retain the prior Base/head conventions. GoHome takeover tests enabled raw input, so Speed=0 or canceling angular contributions cannot mask operator input.

Supported split/rejoin sections have equal path length, internally distinct grids, the same actuator kind at each corresponding ordinal, and matching full head-point screws after sign. This operationally accepts symmetric 1:1 copies such as piston–coaxial rotor–piston. Exact fixed space screws, equal initial end transforms, and common coordinates would give identical serial products; this script measures instantaneous head-point twists with finite tolerances instead, so it does not prove that theorem for an arbitrary build. It rechecks before commands and stops on divergence. Unequal paths, non-coaxial rotary copies, variable ratios, cross-links, and general closed linkages are rejected. Components touching one corridor grid are reported and ignored; they are never commanded. Components reconnecting to two corridor grids are rejected.

Tool swaps use `ToolSwapController`, `ToolRegionScan`, `ToolTopInfo` and `ToolGeometry`. Installed `MyCubeGrid`'s explicit PB `GetCubeBlock` implementation returns a slim block only for armor (null fat block) or an accessible terminal block; it hides non-terminal rotor/hinge tops. Region scanning therefore uses `CubeExists` for occupancy, treats hidden occupied cells as opaque, and cuts only across proven adjacent facing merge-block surfaces. Mounted `Mount.Top` is accepted only if its cell belongs to the marker's region. `[AutoArm Couplers]` in Storage scopes learned top identity/subtype and marker-relative pivot/axes to PB, arm and marker EntityId. A cached position must remain occupied and inside that region; overlapping profile regions, duplicate identities, corrupt geometry and foreign ownership are refused. An unlearned parked profile can coexist with manual mounted control, but cannot be selected for pickup. Each new tool requires one actual mount observation.

Mechanical upstream identity is guarded independently of the temporarily excluded detachable mount. `ToolPoseStep` uses the existing solver, physical bounds and output commit against live marker-relative goals. All selected support ports must be locked before detachment; actual attached-top identity, reciprocal Base and a newly recomputed live coupling pose gate support release. Cached pose agreement cannot substitute for those checks. Actual grid separation precedes endpoint rebuild. Expected swap boundaries preserve weights by physical signed-member identities while rewriting endpoint/grid signatures.

Operations journal mechanical mutations before writes. Cancellation stops without rollback; passive discovery cannot clear recovery. Explicit successful `ToolScan` reconciles it. Swap completion stays Off. Both manual control and every Home pass call `AllowManual` so an unexpected attachment to the excluded mount cannot escape validation. Startup `ZeroSavedDrives` stops same-construct actuators from a PB/arm-scoped identity cache and a uniquely named configured mount before configuration errors can prevent ordinary discovery from acquiring ownership. It does not switch connections or command unrelated drives.
