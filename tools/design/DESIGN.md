---
name: AutoArm documentation
description: Clean technical diagrams with recognizable Space Engineers shapes.
colors:
  ink: "#263e4b"
  muted: "#496370"
  paper: "#f8faf9"
  line: "#d4dfe2"
  wash: "#edf2f3"
  accent: "#815716"
  focus: "#9e6d20"
  light: "#e5edf0"
  steel: "#b4c5cd"
  shade: "#849da9"
  recess: "#314b59"
  amber: "#e9b34c"
  amber-shade: "#c98c30"
  signal: "#58a28d"
  display-glass: "#bddfe1"
  key-border: "#9db1bb"
  library-accent: "#8c611f"
  library-dark: "#172c36"
  terminal-panel: "#202e35"
  terminal-field: "#293941"
  terminal-text: "#d7e9eb"
  terminal-divider: "#50636c"
  terminal-border: "#50606a"
  terminal-selected: "#5b747d"
typography:
  display:
    fontFamily: "Oxanium, sans-serif"
    fontSize: "52px"
    fontWeight: 500
    lineHeight: 1.12
    letterSpacing: "-0.025em"
  headline:
    fontFamily: 'Inter, "Segoe UI", sans-serif'
    fontSize: "25px"
    fontWeight: 600
    lineHeight: 1.3
    letterSpacing: "-0.02em"
  body:
    fontFamily: 'Inter, "Segoe UI", sans-serif'
    fontSize: "16px"
    lineHeight: 1.65
  intro:
    fontFamily: 'Inter, "Segoe UI", sans-serif'
    fontSize: "18px"
    lineHeight: 1.65
  number:
    fontFamily: "Oxanium, sans-serif"
    fontSize: "25px"
    fontWeight: 500
    lineHeight: 1.3
  brand:
    fontFamily: "Oxanium, sans-serif"
    fontSize: "21px"
    fontWeight: 600
    lineHeight: 1.65
  label:
    fontFamily: 'Inter, "Segoe UI", sans-serif'
    fontSize: "13px"
    lineHeight: 1.65
  caption:
    fontFamily: 'Inter, "Segoe UI", sans-serif'
    fontSize: "12px"
    lineHeight: 1.6
  code:
    fontFamily: '"Cascadia Mono", Consolas, monospace'
    fontSize: "0.88em"
    lineHeight: 1.65
  key:
    fontFamily: "Inter, sans-serif"
    fontSize: "18px"
    fontWeight: 600
    lineHeight: 1.65
rounded:
  code-and-mockup: "3px"
  library-preview: "4px"
  keycap: "5px"
spacing:
  inline: "7px"
  label-gap: "10px"
  compact-gap: "12px"
  paragraph: "16px"
  column-gap: "24px"
  visual-gap: "28px"
  section: "60px"
components:
  print-action:
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    padding: "0"
  guide-navigation:
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    padding: "19px 0"
  step-number:
    textColor: "{colors.accent}"
    typography: "{typography.number}"
    padding: "3px 0 0"
  inline-code:
    backgroundColor: "{colors.wash}"
    typography: "{typography.code}"
    rounded: "{rounded.code-and-mockup}"
    padding: "3px 7px"
  endpoint-example:
    backgroundColor: "{colors.wash}"
    rounded: "{rounded.code-and-mockup}"
    padding: "10px 14px"
  movement-key:
    backgroundColor: "{colors.light}"
    textColor: "{colors.ink}"
    typography: "{typography.key}"
    rounded: "{rounded.keycap}"
    padding: "0 13px"
    height: "54px"
---

# Design System: AutoArm documentation

## Overview

**Creative North Star: "Clean technical diagrams with recognizable game shapes"**

This is the built documentation design for `docs/getting-started.html` and the approved reusable SVG library in `docs/assets/blocks`. It records the user's chosen technical illustration style; it does not establish a visual identity for the in-game script. `PRODUCT.md` supplies the durable commitment to recognizable Space Engineers shapes and the title “Getting Started with AutoArm.”

Paper, quiet dividers and readable prose give the mechanical illustrations room. Steel-blue faces, dark recesses and limited amber details identify parts without simulated textures. The guide pairs each numbered action with geometry or a terminal mockup. The supplied nine-step order is local to this guide, not a template for every documentation page.

This is an extracted implementation record, not a seed or a new concept. Source authority is the guide's inline CSS, the library's `manifest.json`, SVG geometry and reuse notes, and `.impeccable/guide-direction.md`. Existing review evidence is `.impeccable/review/desktop.png` (1265 × 6296) and `mobile.png` (375 × 9044); the completed review returned **ship** with no material fixes. No additional rendering or detector pass was performed for this record.

**Key Characteristics:**

- Numbered actions with directly adjacent visual evidence.
- Steel-blue schematic geometry with restrained amber emphasis.
- Oxanium display and terminal lettering; Inter for reading.
- Flat page surfaces, clear mechanical silhouettes and labeled joints.
- Responsive stacking, readable scrolling diagrams and a print layout.

## Colors

The approved palette combines cool steel surfaces and paper with warm amber details. Frontmatter contains the reusable incumbent palette; the sidecar's tonal ramps are derived swatch previews, not additional production colors. Unsupported-example headings use the local semantic color (#91483d); vector crosses and axis guides use (#ad5447).

### Primary

- **Deep Steel Ink** (`ink`): guide text, links and SVG outlines; it anchors the whole documentation set.
- **Steel Blue** (`steel`), **Light Steel** (`light`) and **Steel Shade** (`shade`): lit, midtone and shaded mechanical faces. The light steel also fills movement keycaps.
- **Mechanical Recess** (`recess`): inset cavities, dark hardware and display framing.

### Secondary

- **Reading Amber** (`accent`): numbered steps and guide-link hover. **Focus Amber** (`focus`) makes keyboard focus visible against paper.
- **Hardware Amber** (`amber`) and **Hardware Amber Shade** (`amber-shade`): selected collars, guards and highlighted fields inside terminal mockups. These lighter illustration colors do not replace the darker guide-text accent.
- **Library Amber** (`library-accent`): the existing parts-preview link and focus accent; preserve this local variant when documenting that page.

### Tertiary

- **Signal Green** (`signal`): small indicators on electronic parts.
- **Display Glass** (`display-glass`): the controller and programmable-block screen surfaces.

### Neutral

- **Paper** (`paper`), **Code Wash** (`wash`) and **Divider Steel** (`line`): the reading canvas, inline-code backing and quiet section boundaries.
- **Muted Steel** (`muted`): captions and supporting prose; the SVG library uses the same value for dark mechanical surfaces.
- **Key Border** (`key-border`): keycap edges and diagram leader lines.
- **Library Dark** (`library-dark`): the optional dark backdrop in the asset preview only.
- **Terminal Panel**, **Terminal Field**, **Terminal Text**, **Terminal Divider**, **Terminal Border** and **Terminal Selected** (`terminal-*`): the repeated dark-game-UI vocabulary of the three static mockups. Keep it inside those illustrations.

**The Evidence Accent Rule.** Amber marks step numbers, actionable states and selected mechanical details; it does not fill decorative page panels.

## Typography

**Display Font:** Oxanium, with a sans-serif fallback. **Body Font:** Inter, with Segoe UI and sans-serif fallbacks. Both guide fonts ship as local variable TTF files in `docs/assets/fonts` with swap loading. **Code Font:** Cascadia Mono, Consolas, monospace.

Oxanium's squared forms connect the guide title, wordmark and step numerals to the game. Terminal mockup lettering uses Oxanium at weight 500 converted to SVG paths, so the mockups remain portable. Inter carries step headings, paragraphs, navigation, control labels and guide-diagram labels; it is not limited to body paragraphs. The existing parts-preview page and contact sheet use their system-font stacks.

### Hierarchy

- **Display:** the frontmatter `display` role is the wide-screen title; it steps down at the two documented breakpoints. Keep the balanced two-line title and its restrained weight.
- **Headline:** the `headline` role introduces each action; it is Inter, with tighter leading than body text.
- **Body and Intro:** the `body` and `intro` roles use generous reading leading. Copy stays within 65ch; the introduction uses 67ch.
- **Number and Brand:** Oxanium provides the repeated sequence marker and quiet AutoArm wordmark. Sequence markers use tabular numerals.
- **Label and Caption:** supporting text recedes through size and muted color, while figure captions retain their own reading leading.
- **Code and Key:** monospace code preserves literal names and commands. Inter key lettering keeps the physical keycaps distinct from code examples.

**The Literal Marker Rule.** Render names and commands as code; retain the exact `<MyArmName> - Base`, `<MyArmName> - Head` and `On` spelling in this guide.

## Layout

The guide is a centered reading surface capped at 1160px, with 48px outer gutters on wide screens. A normal step uses a 44px number rail followed by flexible copy and visual columns in a 1:1.12 ratio; the columns have a 24px gap. Section padding is 60px vertically, with dividers between actions. Text and illustration remain one semantic step rather than floating cards.

The built sequence is: **1** place the first actuator; **2** name the base; **3** build the arm; **4** name the head; **5** place a cockpit or controller; **6** place a programmable block; **7** load the script and run `On`; **8** take control; **9** follow documentation. Jump navigation groups steps 1–4, 5–7, 8 and 9. Preserve this user-pinned order for this surface.

Step 3 places the supported front diagram on the left and two unsupported examples on the right: misaligned parallel stages and a piston driving a hinged hydraulic boom. The comparison uses a 1.14:1 column ratio, a 48px gap and 34px top spacing. The front SVG is 520 × 1350 and displays at up to 520px wide. Its bottom-to-top sequence remains base rotor → three-block crossbar → two hinges → two pistons → second three-block crossbar → hinge → rotor → hinge → piston → rotor → hinge → hinge with its axis turned 90° → rotor → drill. The visible crossbar-width and 90° annotations are removed; the geometry remains. This is schematic guide evidence, not an in-game-validated build.

At 800px and below, outer gutters become 24px, title size becomes 42px, and each step becomes a number-plus-copy grid. Visuals stack beneath copy; the number rail becomes 36px, the gap 16px and section padding 44px. The supported/unsupported comparison also stacks, with a 32px gap and an unsupported aside capped at 520px. At 480px and below, gutters become 16px, title size 35px, step headings 22px and body copy 15px. The number rail becomes 28px, the gap 12px and section padding 36px; visuals and controls span both columns.

On the narrowest layout the long front diagram retains a 520px minimum image width within its horizontally scrollable region. Its focusable region, explanatory caption and “Open the full diagram” link preserve label legibility without expanding the page. All other images scale to their containers. The six movement keys form three equal columns. The parts-preview library has its own 640px breakpoint, where descriptions span the row above the paired asset views.

Print CSS targets A4 with 16mm margins, white paper and 10pt body copy. It hides resource navigation, jump navigation, the print action and footer; the title becomes 30pt, step headings 15pt. Regular steps avoid internal page breaks, while the long build step may break. The comparison keeps two equal columns with a 12mm gap; its full-width front figure and each unsupported article avoid internal breaks. Article padding becomes 10px, with illustration heights of 40mm (48mm for misalignment). The isometric image remains capped at 180mm and mockups at 70mm.

**The Readable Geometry Rule.** Reflow text and stack figures; preserve the front diagram's readable labels through local scrolling and its full-diagram link.

## Elevation & Depth

The guide and library preview use no box shadows. Paper, thin dividers, code wash and the darker terminal canvases establish separation. Mechanical depth comes from projected faces, steel tones and dark recesses inside SVGs. The keycaps' thicker bottom edge depicts a physical key, rather than elevation on page sections.

## Shapes

Page sections are open and divided by lines. Small rounding belongs only to the observed code/mockup, library-preview and keycap roles in frontmatter. The drawings use clear cubes, cylindrical rotor heads, hinge forks, piston shafts, drill drums, welder electrodes and a recognizable controller console; retain those silhouettes when extending the library.

Standalone library assets use a transparent 320 × 320 canvas, with geometry individually fitted into a 248 × 248 area and approximately 35 units of padding. They are not at a shared physical scale. Side views are orthographic; isometric views use the same model and a true 30° projection. The mount faces down and the output/head or working tip faces up. Outlines are 2.2 canvas units with round joins and caps and lighter internal details; composed guide geometry limits strokes to 1.4 units and uses non-scaling strokes.

The manifest and embedded metadata provide each illustration's scale and projected mount/head/tip landmarks; hinge metadata identifies its transverse pivot. Shared-scale compositions compensate for each standalone asset's fit scale before aligning anchors. SVGs remain editable vectors without raster images, scripts, filters or external asset dependencies. Include titles and descriptions; keep surrounding labels and arrows separate from reusable part artwork. When repeating inline assets, prefix their IDs and update accessibility references.

**The Schematic Shape Rule.** Preserve recognizable game geometry and projection consistency without presenting the symbols as CAD drawings or exact game bounding volumes.

## Components

### Navigation and print action

Underlined resource links and a text-like print button keep the header quiet. Jump links omit underlines and sit between thin horizontal rules. Links shift to Reading Amber on hover. Guide links, buttons and summaries receive a 3px Focus Amber outline offset by 5px; the scrolling diagram uses a 4px offset. The library's links and inputs use its existing 3px Library Amber outline and 5px offset. Do not infer a persistent active-tab state that the guide does not implement.

### Numbered step

An Oxanium amber numeral aligns with the Inter action heading, body copy and evidence figure. The build step uses the supported/unsupported comparison row; the final step uses an inset text-only continuation. Keep the semantic headings, IDs, image dimensions, descriptive alternative text and captions.

### Endpoint examples and commands

Inline code sits on Code Wash with small rounding. A larger endpoint example is a block-sized code marker preceded by a muted example label. It wraps long identifiers rather than overflowing. The illustrated `Arm 1` prefix is an example, not a required arm name.

### Movement controls

Each control combines a Light Steel keycap, Deep Steel Ink lettering and a muted direction caption. Keycaps are at least 65px wide and 54px high, with a 1px edge and 3px bottom edge. Use real `kbd` elements and preserve the six shown mappings.

### Illustrations and terminal mockups

Transparent part figures have bounded widths and retain their aspect ratios. The front arm has part labels on leader lines and a bottom-to-top reading instruction. Step 6 shows a classical bent-elbow isometric pose: both lower paired hinge heads rotate backward 45° together, carrying the double pistons and joined crossbar; the upper serial chain folds forward and the drill faces forward/down. Beside the programmable block and controller, the 850 × 940 SVG retains all thirteen actuators, exact mounting orientations, sequence and attachments; its displayed height remains capped at 620px. Terminal mockups are dark, static SVG illustrations of the supplied game references, with outlined Oxanium text and Hardware Amber highlights on the exact Name, Argument or Run target. They are not live form fields.

### Unsupported examples

The right-hand aside pairs each support limit with its own schematic SVG: `unsupported-misaligned.svg` and `unsupported-hydraulic.svg`. Its Inter heading is 22px with 1.35 leading; article headings are 17px, weight 600 and 1.4 leading. Thin dividers and 26px vertical padding separate articles. Figures are 250px high (310px for misalignment), contain their geometry, and use semantic crosses and axis guides to identify the unsupported configuration.

### Library view controls

The preview uses native radio buttons for both/side/isometric views and a checkbox for the dark background. Labels and controls remain paired; fieldset legends supply accessible grouping. The switches change presentation, not the asset geometry or palette.

Smooth in-page scrolling is the only motion convention; it switches to automatic scrolling for reduced-motion preference and print. No decorative animation, hover lift or transition timing token exists in the built guide.

## Do's and Don'ts

### Do:

- **Do** preserve the approved steel-blue and amber SVG world and recognizable game-part silhouettes.
- **Do** pair each guide action with its relevant geometry, terminal target or movement mapping.
- **Do** keep the supplied nine-step order and matching front/isometric arm sequence on this guide.
- **Do** retain exact endpoint naming syntax, readable labels, keyboard focus and full-diagram links.
- **Do** use the manifest's scale and anchors when composing reusable parts.
- **Do** maintain stacked mobile layouts, local front-diagram scrolling and the existing print treatment.

### Don't:

- **Don't** replace the user's chosen diagram style with a new visual identity during a documentation extension.
- **Don't** treat individually fitted library assets as a common physical scale.
- **Don't** add operation effects or photographic textures to the approved schematic part symbols.
- **Don't** shrink the long front diagram until its labels become unreadable on a phone.
- **Don't** describe schematic geometry or terminal mockups as a validated in-game construction or live UI.
- **Don't** apply this documentation design record to the script's in-game behavior or unrelated repository work.
