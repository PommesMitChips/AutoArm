# AutoArm diagram parts

Schematic artwork for the Getting Started guide. Eight parts, each in side and isometric views, drawn as editable vectors with transparent backgrounds.

| Part | Side | Isometric |
| --- | --- | --- |
| Standard rotor | [SVG](rotor-side.svg) | [SVG](rotor-isometric.svg) |
| Hinge | [SVG](hinge-side.svg) | [SVG](hinge-isometric.svg) |
| Partly extended piston | [SVG](piston-side.svg) | [SVG](piston-isometric.svg) |
| Ship drill | [SVG](drill-side.svg) | [SVG](drill-isometric.svg) |
| Ship welder | [SVG](welder-side.svg) | [SVG](welder-isometric.svg) |
| Light armor block | [SVG](block-side.svg) | [SVG](block-isometric.svg) |
| Programmable block | [SVG](programmable-block-side.svg) | [SVG](programmable-block-isometric.svg) |
| Control seat | [SVG](controller-side.svg) | [SVG](controller-isometric.svg) |

See all sixteen on the [vector contact sheet](contact-sheet.svg), browse them in [the preview](preview.html), or use [the ZIP](autoarm-parts-svg.zip).

## Conventions

- All standalone assets use a `320 × 320` canvas with at least 35 units of padding. Art is individually fitted inside a `248 × 248` area; different assets are **not at a shared physical scale**.
- Side views are orthographic. Isometric views use a true 30° projection. Both views project the same geometric model.
- The mounting end is at the bottom; the output/head or working tip points upward. Rotate the whole asset to suit an arm diagram.
- The programmable block's orthographic view exposes its display face for identification.
- Outlines are 2.2 canvas units, with lighter internal details. Steel blue distinguishes mechanical surfaces; amber marks selected collars/guards. No tool-operation effects are shown.
- The rotor is the standard round-head variant. The block is a generic light armor cube. These illustrations use vanilla large-grid block icons as visual references, but simplify the models and proportions. They are guide symbols, not CAD drawings or bounding-volume diagrams.
- Files contain no raster images, fonts, scripts, filters or external dependencies. Titles and descriptions are embedded for accessibility. Labels and arrows belong in the surrounding guide.
- `data-part` attributes identify components such as `base`, `head`, `shaft`, `guard` and `electrode`. The `artwork` group contains the visible geometry.

## Reuse

Insert the standalone SVG directly in a document or vector editor. To recolor a part, replace a palette value across its shapes. Keep matching values across the set.

`manifest.json` and each SVG's metadata record projected attachment landmarks and the model-to-canvas scale. To compose parts at a shared schematic scale, size each asset in inverse proportion to its `scale`, then align the desired `mount`, `head` or `tip` anchors. These landmarks describe the illustration rather than exact game geometry. The hinge's `axis` is its transverse pivot center.

For inline HTML, prefix the SVG's `title`, `desc` and `artwork` IDs per instance, and update `aria-labelledby`, to avoid duplicate IDs. Using an image element avoids that issue:

```html
<img src="assets/blocks/hinge-isometric.svg" width="160" height="160"
     alt="Hinge with its moving head pointing upward">
```

## Rebuild

Run `python docs/assets/blocks/build.py` from the repository root. The generator uses only Python's standard library and rebuilds the sixteen SVGs, contact sheet, manifest and ZIP. The preview page and optional PNG contact sheet are maintained separately.

Visual references were inspected from the installed game's `Content/Textures/GUI/Icons/Cubes` directory: `motor.dds`, `RotorPart.dds`, `Hinge.dds`, `HingeHead.dds`, `Piston.dds`, `drill.dds`, `Welder.dds`, `light_armor_cube.dds`, `ProgrammingBlock.dds` and `Cockpit.dds`. Those source images are not included in the asset pack. Space Engineers is a game by Keen Software House; this is independently authored AutoArm documentation artwork.
