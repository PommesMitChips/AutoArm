# AutoArm 1.1

AutoArm controls robotic arms in Space Engineers. It discovers the arm's joints,
lets you move the tool head from a cockpit, and supports multiple arms from one
programmable block. ToolSwap adds automatic tool changes.

## Getting started

1. Build an arm using rotors, hinges and pistons.
2. Name its first actuator `Arm 1 - Base` and its tool or final block `Arm 1 - Head`.
   Intermediate joints can keep their normal names.
3. Place a cockpit or controller and a programmable block on the same construct.
4. Paste [AutoArm_Compact.txt](stable/AutoArm_Compact.txt) into the programmable block.
5. Run `On`, then use WASD and C/Space from the cockpit to move the arm.

Use a different name for each arm, such as `Arm 2 - Base` and `Arm 2 - Head`.

The [Getting Started guide](docs/GETTING_STARTED.md) shows the full setup with
illustrations. See [Troubleshooting](docs/TROUBLESHOOTING.md) if an arm fails to
start or a tool will not attach.

## Scripts

Paste the compact file into a programmable block. The source file is the readable
version of the same script.

| Script | Purpose | Files |
| --- | --- | --- |
| AutoArm | Arm control | [Compact](stable/AutoArm_Compact.txt) · [Source](stable/AutoArm_Source.txt) |
| ToolSwap | Automatic tool changes | [Compact](stable/AutoArm_ToolSwap_Compact.txt) · [Source](stable/AutoArm_ToolSwap_Source.txt) |
| Collision Safety | Collision checks for the construct and registered arms | [Compact](stable/AutoArm_Collision_Compact.txt) · [Source](stable/AutoArm_Collision_Source.txt) |
| Motion Bench | Motion tests and measurements | [Compact](stable/AutoArm_Bench_Compact.txt) · [Source](stable/AutoArm_Bench_Source.txt) |
| Survey | Inspect an arm's geometry and configuration | [Compact](stable/AutoArm_Survey_Compact.txt) · [Source](stable/AutoArm_Survey_Source.txt) |

Install ToolSwap in a second programmable block on the same construct. Tools
start parked on merge-block stands, with a rotor base on each tool and a matching
rotor head on the arm. Name the tool references `Arm 1 - Head 1`,
`Arm 1 - Head 2`, and so on. Run `Tool 1`, `Tool 2` or `Park` on AutoArm.
[Configuration examples](docs/examples) include an arm with ToolSwap.

## Build

Requires PowerShell 7, the .NET 10 SDK and the DLLs in Space Engineers' `Bin64`
folder. Use `-GameBin` if that folder is somewhere other than the default Steam
location. Run these commands from the repository root:

```powershell
./Build.ps1 -Track experimental -Scripts AutoArm
./Build.ps1 -Track experimental -Scripts AutoArm,ToolSwap
./Build.ps1 -Track stable -Collection Release -Test
```

Builds write source and compact files into the selected track. Edit the files in
`src/` to make changes. See [tools](tools/README.md) for build options and tests.

## Guides and artwork

- [Getting Started](docs/GETTING_STARTED.md) · [HTML version](docs/getting-started.html)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Configuration examples](docs/examples)
- [SVG panels](docs/assets/workshop/svg-panels/README.md)
- [Panel assembler](tools/panel-assembler/dist/AutoArm-Panel-Assembler.html) · [Instructions](tools/panel-assembler/README.md)

## Repository layout

```text
stable/          Tested scripts, with editable files in src/
experimental/    Work in progress and prototypes, with editable files in src/
tools/           Build tools, tests, asset generators and the panel assembler
docs/            Guides and configuration examples
docs/assets/     Diagrams, models, panels and image exports
```
