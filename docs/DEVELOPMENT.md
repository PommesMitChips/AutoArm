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

The current compact script has 99,772 UTF-16 characters, leaving 228 under the PB's 100,000-character limit; a longer `ArmName` consumes some of that allowance. UTF-8 file bytes are not the PB's character count. The generated implementation uses short identifiers and keeps the editable `ArmName` header readable. Configured velocity and acceleration caps are not physical momentum measurements. Available torque/force, travel limits and game physics determine achievable motion.

The v2.5 suite passes **174,768 assertions**. Coverage includes topology discovery, signed parallel groups, additive/manual-priority input, gravity bias, movement frames, facing-merge boundaries, swap/park sequences, hidden non-terminal tops, learned couplers across restart, cache scope/geometry, wrong tops, actual coupling-pose gates, cancellation/restart, startup drive stopping and Home attachment faults. Live SE physics, actual instruction costs and loaded merge timing remain unverified.

