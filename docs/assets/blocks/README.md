# Block diagrams

Side and isometric SVGs for AutoArm guides.

| Part | Side | Isometric |
| --- | --- | --- |
| Rotor | [SVG](rotor-side.svg) | [SVG](rotor-isometric.svg) |
| Hinge | [SVG](hinge-side.svg) | [SVG](hinge-isometric.svg) |
| Piston | [SVG](piston-side.svg) | [SVG](piston-isometric.svg) |
| Drill | [SVG](drill-side.svg) | [SVG](drill-isometric.svg) |
| Welder | [SVG](welder-side.svg) | [SVG](welder-isometric.svg) |
| Armor block | [SVG](block-side.svg) | [SVG](block-isometric.svg) |
| Programmable block | [SVG](programmable-block-side.svg) | [SVG](programmable-block-isometric.svg) |
| Control seat | [SVG](controller-side.svg) | [SVG](controller-isometric.svg) |

[Preview](preview.html) · [Contact sheet](contact-sheet.svg) · [Download ZIP](autoarm-parts-svg.zip)

Open the files in a vector editor to resize, rotate or recolour them. Each has a
320 × 320 transparent canvas. The parts are sized to fit their canvases;
`manifest.json` contains scale and attachment points for assembling them.

To rebuild the diagrams, run this from the repository root:

```powershell
./tools/Build.ps1 -Scripts Blocks
```

Requires Python. The PNG contact sheet and preview page are separate files.
