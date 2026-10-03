# AutoArm 1.1

Robotic arm control for Space Engineers programmable blocks, with automatic
actuator discovery, cockpit control, multiple arms, saved poses and optional
ToolSwap and collision Safety services.

Start with the [illustrated Getting Started guide](docs/GETTING_STARTED.md).

## Install

1. Stop the existing controller before replacing it.
2. Paste [AutoArm](stable/AutoArm_Compact.txt) into the arm's programmable block.
3. Name the first joint `Arm 1 - Base` and the moving endpoint `Arm 1 - Head`.
   Intermediate joints can keep their normal names. Different arms use different
   prefixes; supported parallel stages must satisfy the topology checks.
4. Run `On`. Use WASD and C/Space from your cockpit; Q/E rolls the head.
5. For detachable tools, install [ToolSwap](stable/AutoArm_ToolSwap_Compact.txt)
   in a second PB. Start with each tool merged to its stand, rotor bases on the
   tools and one compatible loose rotor head on the arm. Number the tool markers
   `Arm 1 - Head 1`, `Arm 1 - Head 2`, etc. Run `Tool 1`, `Tool 2` or `Park` on AutoArm.

Keep compatible Custom Data: AutoArm uses Format 7, ToolSwap Format 2. Release
**1.1**, configuration formats and link protocol 5 are separate identifiers.
Installed game/script-browser copies are not updated by repository builds.

## Stable scripts

| Script | Compact PB file | Readable source |
| --- | --- | --- |
| AutoArm | [Compact](stable/AutoArm_Compact.txt) | [Source](stable/AutoArm_Source.txt) |
| ToolSwap | [Compact](stable/AutoArm_ToolSwap_Compact.txt) | [Source](stable/AutoArm_ToolSwap_Source.txt) |
| Collision Safety | [Compact](stable/AutoArm_Collision_Compact.txt) | [Source](stable/AutoArm_Collision_Source.txt) |
| Motion Bench | [Compact](stable/AutoArm_Bench_Compact.txt) | [Source](stable/AutoArm_Bench_Source.txt) |
| Read-only Survey | [Compact](stable/AutoArm_Survey_Compact.txt) | [Source](stable/AutoArm_Survey_Source.txt) |

Stable was initialized from the current non-prototype working tree at the user's
direction. Experimental starts from the same scripts and retains WIP planning,
passing, rate-reallocation and self-pair experiments. Historical Git tags keep
their original names; they are not new Workshop release numbers.

## Guides and tools

- [Getting Started](docs/GETTING_STARTED.md) · [HTML guide](docs/getting-started.html)
- [Collision Safety](docs/COLLISION.md) · [Troubleshooting](docs/TROUBLESHOOTING.md)
- [External script integration](docs/MODULES.md) · [Configuration examples](docs/examples)
- [Motion Bench](docs/IN_GAME_BENCH.md) · [Read-only Survey](docs/IN_GAME_SURVEY.md)
- [Workshop material](docs/steam-workshop)
- [Panel assembler](tools/panel-assembler/dist/AutoArm-Panel-Assembler.html)
  · [Instructions](tools/panel-assembler/README.md)
- [Editable SVG panels](docs/assets/workshop/svg-panels)
  · [Reusable separator](docs/assets/workshop/svg-panels/common/separator.svg)

Collision Safety models conservative construct geometry and registered arms,
with bounded contact allowances. Terrain, unrelated ships and general route
planning are outside the stable service. Begin with low speeds and confirm your
build in the game; simulation checks do not establish live physics behavior.

## Build

Use PowerShell 7, the .NET 10 SDK and installed Space Engineers assemblies:

```powershell
./Build.ps1 -Track experimental -Scripts All
./Build.ps1 -Track experimental -Scripts AutoArm,ToolSwap
./Build.ps1 -Track stable -Collection Release -Test
./Build.ps1 -Track experimental -Collection JointPlanner
./Build.ps1 -Track stable -Scripts AutoArm -SourceOnly
./Build.ps1 -Collection Assets -Python python
./Build.ps1 -List
```

`Core` selects AutoArm, ToolSwap and Collision; `Diagnostics` selects Bench and
Survey; `Release` selects all five. `Prototypes` and `JointPlanner` are experimental
only. Existing WIP snapshots are the prototype build inputs;
`-RefreshPrototypes` explicitly regenerates them against the current core and may
expose unresolved PB-size or compatibility limits. `-Test` includes the release scripts required by the complete readable and
compact suite. `-WhatIf` previews the build; `-GameBin` overrides the game's Bin64
location. Asset dependencies are described in [tools](tools/README.md).

Builds write only the selected track and never promote automatically. Edit
`experimental/src/<script>/`; generated readable/compact scripts are not masters.
Each track's `release.json` owns its release label; builds expand `@@VERSION@@`
placeholders without changing protocol/configuration numbers. Each source folder's `AGENTS.md` describes its files and responsibilities.
Shared fragments are under `src/shared`. Explicit promotion must include matching
sources, dependencies and outputs, with their validation evidence.

## Repository layout

```text
stable/          Approved sources and generated PB scripts
experimental/    WIP sources, prototypes and generated PB scripts
tools/           Build helpers, tests, generators and the panel assembler
docs/            User-facing guides and configuration examples
docs/assets/     Block graphics, assembled models, panels and graphic exports
```

Developer records are under [tools/development](tools/development).
See [attribution](docs/ATTRIBUTION.md) and [history](docs/HISTORY.md) for origins
and earlier development.
