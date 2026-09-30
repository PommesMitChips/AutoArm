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

Edit the fragments in `src/`. The build regenerates `AutoArm_Source.txt` and `AutoArm_Compact.txt`, checks C# 6 against installed SE APIs, verifies token round-trip and identical method IL, and runs the simulated PB harness. Tests also regenerate `examples/CustomData.ini`. [`src/AutoArm.Seam.md`](../src/AutoArm.Seam.md) describes the implementation boundaries.

The current compact script has 99,777 UTF-16 characters, leaving 223 under the PB's 100,000-character limit; a longer `ArmName` consumes some of that allowance. UTF-8 file bytes are not the PB's character count. The generated implementation uses short identifiers and keeps the editable `ArmName` header readable. Configured velocity and acceleration caps are not physical momentum measurements. Available torque/force, travel limits and game physics determine achievable motion.

The v2.8.1 simulated suite passes **178,062 assertions** and covers topology discovery, signed parallel groups, additive/manual-priority input, gravity bias, movement frames and pose commands. Tool cases exercise occupied-cell facing-merge regions, named rotor-only tool-side bases, automatic arm-end discovery without reference names or offsets, hidden arm parts, cold headless startup/manual movement/pickup, first-use swaps without per-tool teaching, saved headless restart, exact identities and coupling poses, support/body partitions, foreign ownership, cancellation/restart, invalid startup stopping and Home interlocks. One-shot startup tests check current settings, real stopped observations, mounted/bare control, immediate tool selection, active-swap exclusion, automatic control after mounting, already-mounted selection and startup cancellation/failure. Live SE physics, actual instruction costs and loaded merge timing remain unverified.

Packing uses one-character BMP identifiers for script-owned members and scoped locals, preserves enum/API names, shortens numeric and string spelling only when exact values match, and verifies token reversal plus identical method IL. Namespace using aliases cannot be inserted into the current class-body PB script. Production targets C# 6; newer pattern matching is not used. Readable source shares pose targets, capture/reset, angle wrapping, finite checks and frame conversion; saved Home remains native joint-space control.
Declaration compression also removes redundant private modifiers and uses inferred local/array types only when Roslyn proves identical types. Final method IL is compared against the original readable source compilation. Control shares task-matrix formation and scalar component/bound-hit logic; numerical operation order and native Home routes are retained. Actual game instruction cost remains unprofiled.

Tool discovery starts from the Base marker, excludes the hull and configured tool couplers, and incrementally scans reachable moving grids. Connected top parts are excluded from hidden-cell candidates. A single 1-cell or 3x2x3 rotor-shaped region and one plausible mounting face determine pivot and signed axis. Unknown part identity and azimuth are confirmed only after attachment; first acquisition requires unlimited rotor limits and temporarily unlocks/restores Rotor Lock. Multiple candidates or ambiguous faces refuse. New graph and postprocessing work has inventory/link bounds and budget checks. Coupler pose storage is unnecessary; startup rescans instead. Configuration Format 5 omits Instructions, Tools.Mount and ArmTip.

Further packing unqualifies imported type names, converts single-return methods to C#6 expression bodies and fuses adjacent equivalent field declarations while preserving initializer/metadata order. Exact original-source IL validation remains required.
