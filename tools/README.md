# Repository tools

Use root `Build.ps1` for PB builds. `build/Recipes.psm1` contains the original
host adapters, specialization, validated packing and guarded prototype recipes.

| Folder or file | Purpose |
| --- | --- |
| `ScriptPack/` | C#6/API checking and semantic/token/IL-checked compaction. |
| `PBCompileChecks/` | Installed SE type-safety and resource/memory rewriting. |
| `ArmTests/` | Readable and actual compact regression harness and fixtures. |
| `panel-assembler/` | Offline panel editor; shipped HTML and ZIP are in dist. |
| `asset-builders/` | Graphics/model generators; outputs go to docs/assets. |
| `design/` | Design guidance and internal visual-workflow records. |
| `Test-JointPlanner.ps1` | Prototype simulation and optional profiling. |
| `PivotEvidence.ps1` | Empirical pivot evidence utility. |

PB tools need PowerShell 7, .NET 10 and installed game assemblies. Asset builders
need Python and Pillow; outlined labels also need fontTools. The .mjs converters
need Node.js and sharp, whose module path may be passed as their first argument.

`Build.ps1 -Collection Assets` refreshes the existing SVG pack without discarding
manual text/artwork edits, then rebuilds the standalone assembler. Individual
full generators are in asset-builders; output paths are repository-relative.
Legacy PNG-from-BBCode generators require their historical copy inputs, which
have been removed. The existing editable SVG panels and assembler remain usable;
the default Assets collection preserves those SVGs and rebuilds the assembler.

The preserved WIP `*.Program.cs.txt` files are the normal prototype build inputs.
`-RefreshPrototypes` applies the original guarded recipes to current core sources.
The reallocation refresh currently exceeds the 100,000-character PB limit;
its preserved WIP snapshot compacts and validates normally.
