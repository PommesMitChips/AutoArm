# Getting Started diagrams

Artwork used in the AutoArm Getting Started guide.

| File | Illustration |
| --- | --- |
| `rotor-on-grid.svg` | Base rotor on a grid |
| `arm-front.svg` | Example arm in front view |
| `arm-isometric-pb.svg` | Folded arm with a programmable block and control seat |
| `unsupported-misaligned.svg` | Misaligned parallel stages |
| `unsupported-hydraulic.svg` | Hydraulic linkage |
| `name-base-terminal.svg` | Naming the base in the terminal |
| `name-head-terminal.svg` | Naming the head in the terminal |
| `run-terminal.svg` | Running the script with `On` |

Open the SVGs in a vector editor to edit them. Component positions and rotations
are listed in `manifest.json`.

To rebuild the artwork, run these commands from the repository root:

```powershell
./Build.ps1 -Scripts Blocks
./Build.ps1 -Scripts Models
```

Requires Python and fontTools. To rebuild only the arm diagrams and keep the
terminal mockups, use:

```powershell
python tools/asset-builders/models/build.py --diagrams-only
```
