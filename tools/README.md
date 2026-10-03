# Tools

Run `Build.ps1` from the repository root to build scripts and assets.

## Build scripts

Script builds require PowerShell 7, the .NET 10 SDK and the DLLs in Space
Engineers' `Bin64` folder for API and compiler checks. Set `-GameBin` to that folder
if it is outside the default Steam location.

```powershell
./Build.ps1 -Track experimental -Scripts All
./Build.ps1 -Track experimental -Scripts AutoArm,ToolSwap
./Build.ps1 -Track stable -Collection Release
./Build.ps1 -Track experimental -Scripts AutoArm -SourceOnly
./Build.ps1 -List
```

| Option | Use |
| --- | --- |
| `-Track` | Choose `stable` or `experimental`. Default: `experimental`. |
| `-Scripts` | Build one script, a comma-separated list, or `All`. |
| `-Collection` | Build a set of scripts; see the table below. |
| `-SourceOnly` | Generate and compile-check readable source without creating a compact file. |
| `-Test` | Build the release scripts and run the regression tests. |
| `-GameBin` | Set the path to Space Engineers' `Bin64` folder. |
| `-WhatIf` | Show what would be built. |
| `-List` | List available scripts and collections. |

| Collection | Scripts |
| --- | --- |
| `Core` | AutoArm, ToolSwap, Collision |
| `Diagnostics` | Bench, Survey |
| `Release` | All five release scripts |
| `Prototypes` | All experimental prototypes |
| `JointPlanner` | JointPlannerArm, Planner |

Prototype builds use the sources in `experimental/src/`. Use `-RefreshPrototypes`
to rebuild the prototypes from the current core scripts. The Reallocation
refresh currently exceeds the programmable block's 100,000-character limit.

## Build assets

Requires Python and Pillow. Update the SVG pack and panel assembler with:

```powershell
./Build.ps1 -Collection Assets
```

Use `-Python` to choose a Python executable. Generated artwork goes into
`docs/assets/`; the assembler's HTML and ZIP go into `tools/panel-assembler/dist/`.

The individual generators are in `asset-builders/`. Block diagrams use Python's
standard library. Model terminal labels also need fontTools. The `.mjs` image
converters use Node.js and sharp.

## Tests

```powershell
./Build.ps1 -Track experimental -Collection Release -Test
node --test tools/panel-assembler/core.test.cjs
```

## Files

| Folder or file | Purpose |
| --- | --- |
| `build/` | Script assembly and build recipes |
| `ScriptPack/` | C# checks and script compaction |
| `PBCompileChecks/` | Compile checks against the game assemblies |
| `ArmTests/` | Arm control and tool-swap tests |
| `panel-assembler/` | Arrange SVG panels, change colours and export images |
| `asset-builders/` | Generate diagrams, models and Workshop images |
| `design/` | Design notes |
| `Test-JointPlanner.ps1` | Joint planner simulation and profiling |
| `PivotEvidence.ps1` | Collect pivot measurements |
