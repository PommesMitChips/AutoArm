# Development

Development requires Windows, the .NET 10 SDK, the .NET Framework reference assemblies, and a local Space Engineers installation. Game assemblies and model assets are not included.

From this repository:

```powershell
.\tools\Build-AutoArm.ps1 -Test
```

For a nondefault game location:

```powershell
.\tools\Build-AutoArm.ps1 -GameBin 'D:\SteamLibrary\steamapps\common\SpaceEngineers\Bin64' -Test
```

Edit the fragments in `src/`. The build regenerates the source and compact files for both the arm and ToolSwap PBs, checks C# 6 against installed SE APIs, verifies token round-trip and identical method IL, and runs the simulated PB harness. Tests also regenerate `examples/CustomData.ini`. [`src/AutoArm.Seam.md`](../src/AutoArm.Seam.md) describes the implementation boundaries.

The current compact scripts contain 79,911 UTF-16 characters (arm; 20,089 free) and 51,414 (ToolSwap; 48,586 free). Each PB has its own 100,000-character allowance. UTF-8 file bytes are not the PB character count. Velocity and acceleration settings are not physical momentum measurements; torque/force, travel limits and game physics determine achievable motion.

The v3.1 suite passes **179,223 simulated assertions**. Existing control tests retain discovery, signed parallel groups, additive/manual-priority input, gravity bias, frames, Home and pose commands. Native rotor geometry tests run against the ToolSwap script. The former monolithic tool tests are replaced by two-host tests with actual IGC queues: mounted/bare startup, final hinges, whole approach/insertion paths, automatic resume, parking, signed/multi-cell parts and locks, stopped attachment fences, actuator-writer ownership, one-way message loss, unavailable peers, replay after Stop, wrong actual parts, invalid equipment/configuration, read-only ToolInfo and pending-attachment restart recovery. Standalone path tests exercise ordered ideal-piston movement, measured settling, full-list validation, limits, Stop/pilot cancellation and Hold takeover. Live game physics, actual instruction costs and loaded merge timing remain unverified.

See [MODULES.md](MODULES.md) for the transport and path contract. AutoArm owns velocities, targets, topology and Home; ToolSwap owns docking geometry and equipment operations. Shared math, rotor-part descriptors and message encoding are built into both scripts from one source. ToolSwap geometry does not carry the arm solver or its joint-control state. A collision service is not implemented.

Packing uses one-character BMP identifiers for script-owned members and scoped locals, preserves enum/API names, shortens numeric and string spelling only when exact values match, and verifies token reversal plus identical method IL. Namespace using aliases cannot be inserted into the current class-body PB script. Production targets C# 6; newer pattern matching is not used. Readable source shares pose targets, capture/reset, angle wrapping, finite checks and frame conversion; saved Home remains native joint-space control.
Declaration compression also removes redundant private modifiers and uses inferred local/array types only when Roslyn proves identical types. Final method IL is compared against the original readable source compilation. Control shares task-matrix formation and scalar component/bound-hit logic; numerical operation order and native Home routes are retained. Actual game instruction cost remains unprofiled.

Tool discovery starts from the Base marker, excludes the hull and configured tool couplers, and incrementally scans reachable moving grids. Connected top parts are excluded from hidden-cell candidates. A single 1-cell or 3x2x3 rotor-shaped region and one plausible mounting face determine pivot and signed axis. Unknown part identity and azimuth are confirmed only after attachment; first acquisition requires unlimited rotor limits and temporarily unlocks/restores Rotor Lock. Multiple candidates or ambiguous faces refuse. New graph and postprocessing work has inventory/link bounds and budget checks. Coupler pose storage is unnecessary; startup rescans instead. Arm Format 6 and ToolSwap Format 1 omit Instructions and ArmTip; only tool-side bases require Mount names.

Further packing unqualifies imported type names, converts single-return methods to C#6 expression bodies and fuses adjacent equivalent field declarations while preserving initializer/metadata order. Exact original-source IL validation remains required.

Tool revision tests additionally verify arm-default travel above 1 m/s despite lower manual correction caps, slow line entry, no premature endpoint acknowledgement at low speed, preserved parked pose after a quarter-turn and ToolSwap restart, declared topology pauses and automatic activation after rebuild. Protocol v4 adds per-waypoint speed/line policies and expected transitions; configurations remain Format 6/1.
