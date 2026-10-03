---
name: AutoArm panel assembler
description: An offline steel-and-amber workspace for arranging the approved guide panels.
colors:
  page: "#172c36"
  surface: "#202e35"
  field: "#293941"
  line: "#50636c"
  ink: "#e5edf0"
  muted: "#b4c5cd"
  accent: "#e9b34c"
  accent-hover: "#f1c46c"
  surface-active: "#314b59"
  tile-line: "#354b57"
  canvas: "#1b2838"
  error: "#ffb9ae"
  selection-hover: "#f49a36"
  selection-staged: "#ffe45d"
typography:
  display:
    fontFamily: "Oxanium, sans-serif"
    fontSize: "25px"
    fontWeight: 500
    lineHeight: 1.15
  title:
    fontFamily: 'Inter, "Segoe UI", sans-serif'
    fontSize: "15px"
    fontWeight: 700
    lineHeight: 1.3
  body:
    fontFamily: 'Inter, "Segoe UI", sans-serif'
    fontSize: "13px"
    fontWeight: 400
    lineHeight: 1.45
  caption:
    fontFamily: 'Inter, "Segoe UI", sans-serif'
    fontSize: "12px"
    fontWeight: 400
    lineHeight: 1.45
  label:
    fontFamily: 'Inter, "Segoe UI", sans-serif'
    fontSize: "11px"
    fontWeight: 400
    lineHeight: 1.45
  code:
    fontFamily: "Consolas, monospace"
    fontSize: "11px"
    lineHeight: "normal"
rounded:
  square: "0"
  control: "3px"
  overlay: "4px"
spacing:
  tight: "5px"
  compact: "6px"
  inline: "7px"
  grid: "8px"
  field-gap: "10px"
  compact-pane: "12px"
  section: "16px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.page}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "7px 9px"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
    textColor: "{colors.page}"
  button-secondary:
    backgroundColor: "{colors.field}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "7px 9px"
  button-secondary-hover:
    backgroundColor: "{colors.surface-active}"
  input-search:
    backgroundColor: "{colors.field}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "6px 8px"
  pane-tab:
    backgroundColor: "{colors.field}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "7px 9px"
  pane-tab-active:
    backgroundColor: "{colors.surface-active}"
  group-tab:
    backgroundColor: "{colors.field}"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    padding: "7px 9px"
  group-tab-active:
    backgroundColor: "{colors.surface-active}"
  library-tile:
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.square}"
    padding: "7px 3px"
  selection-controls:
    backgroundColor: "{colors.field}"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.overlay}"
    padding: "13px 14px"
    width: "285px"
  toast:
    backgroundColor: "{colors.field}"
    textColor: "{colors.ink}"
    typography: "{typography.caption}"
    rounded: "{rounded.overlay}"
    padding: "13px 14px"
---

# Design System: AutoArm panel assembler

## Overview

**Creative North Star: "Flat steel-and-amber Operate workspace"**

The assembler extends the approved AutoArm documentation world into a compact offline utility. Oxanium identifies the tool; Inter carries filenames, labels and controls. Flat steel surfaces keep the native panel artwork prominent, while amber marks actions, focus and placement feedback.

The workspace fits the window and gives the library, assembly settings and preview their own scrolling regions. A compact toolbar keeps template and export actions available. Selection editing uses a visible draft and explicit confirmation; the preview exposes output dimensions beside its heading.

This local implementation record is grounded in `style.css`, `index.html`, `app.js`, `core.js` and `build.py`, with the project-root `PRODUCT.md` and `DESIGN.md` supplying the incumbent identity. The direction is `.impeccable/panel-assembler-direction.md` at the project root. Existing review evidence is in `.impeccable/review/panel-assembler/`: `desktop.png`, `mobile.png`, `mobile-library.png`, `selection.png` and `poster.png`. The finish verdict is **ship**, with all three review fixes resolved and no remaining fixes. This record adds no new rendering or concept round.

**Key Characteristics:**

- Flat steel surfaces with amber actions and visible keyboard focus.
- Filename-first thumbnail tiles and compact labelled controls.
- Window-fit panes with independent scrolling and mobile pane navigation.
- Transparent native SVG assemblies with zoom, pan and expanding Posters.
- Orange hover targets, yellow staged membership and explicit confirmation.

## Colors

Cool steel provides the workspace structure; warm amber carries interaction. Frontmatter records the actual reusable UI and selection colors. Sidecar tonal ramps are derived swatch previews, not additional production colors.

### Primary

- **Action Amber** (`accent`): export and confirmation buttons, focus outlines, active borders and shared-edge guides. **Amber Hover** (`accent-hover`) lightens the primary button on hover.

### Secondary

- **Hover Orange** (`selection-hover`) identifies prospective selection targets; **Staged Yellow** (`selection-staged`) marks the current draft. These are separate phases, not replacement artwork paints.
- **Error Peach** (`error`) signals invalid fields and error notifications.

### Neutral

- **Deep Steel** (`page`), **Panel Steel** (`surface`) and **Field Steel** (`field`) distinguish the workspace, settings pane and controls.
- **Light Steel Ink** (`ink`) carries primary text; **Muted Steel** (`muted`) carries field labels, dimensions and explicit placeholders. Placeholders retain full opacity; the shipped search placeholder has 6.73:1 contrast against its field.
- **Divider Steel** (`line`) bounds fields and panes; **Tile Divider** (`tile-line`) separates repeated tiles and rows.
- **Active Recess** (`surface-active`) is the common hover/selected fill. **Artwork Canvas** (`canvas`) underlies thumbnails and the dark preview checkerboard.

**The Draft Color Rule.** Orange previews a target and yellow records staged membership; only Confirm selection commits the draft. These overlays never become export artwork.

## Typography

**Display Font:** Oxanium, sans-serif fallback. **Body Font:** Inter, Segoe UI and sans-serif fallbacks. Both fonts are embedded in the standalone build. **Code Font:** Consolas, monospace, for editable hex values.

The utility uses a compressed hierarchy rather than the guide's reading scale. The title retains the incumbent squared display face; labels and native controls use Inter without uppercase decoration.

### Hierarchy

- **Display:** the tool heading uses the frontmatter display role and reduces to 22px at the narrowest breakpoint.
- **Title:** bold pane and section headings use the title role.
- **Body:** normal controls and preview empty-state text use the body role.
- **Caption:** checkboxes, named colour rows, supporting text and notifications use the caption role.
- **Label:** field labels, filenames, dimensions and compact controls use the label role; library filenames become 12px on mobile.
- **Code:** hex fields use the code role. Numeric inputs, dimensions and zoom readouts use tabular figures where implemented.

## Layout

The body occupies (100dvh) and does not scroll. Header and wrapping toolbar remain above a flexible three-column workspace: library (260px), settings (335px), then the remaining preview width. At (1600px) and above the fixed panes become (285px/355px); at (1100px) and below they become (225px/305px), with denser pane padding.

The library tile grid uses auto-fill columns with a (90px) minimum, the frontmatter grid gap and contained thumbnails (108px high). Filenames sit above artwork, truncate with an ellipsis and expose the full filename through the tile's title. Settings use paired or triple fields, thin section dividers and a scrolling body below the group controls. Pane interiors typically use the section or compact-pane spacing roles.

At (900px) and below, three labelled pane tabs replace the simultaneous columns; the chosen pane fills the available workspace and Preview is the initial pane. At (540px) and below, the title and toolbar become denser, Template spans a row, and preview padding reduces again. Dimensions remain inline with the Preview heading; zoom and backdrop controls wrap as needed.

Selection controls and notifications sit at the bottom right with (16px) viewport offsets. During colour editing, the toast moves above the selection controls. Overlays are width-constrained to the viewport.

**The Local Scroll Rule.** Keep the workspace fitted to the window; scroll library contents, settings and artwork inside their own panes instead of lengthening the document.

## Elevation & Depth

Panes, tiles and normal controls remain flat. Tonal differences, thin borders and the preview checkerboard provide separation. The preview well is darker than the workspace; the selectable light backdrop changes viewing contrast only. Shadows belong to temporary overlays: selection controls and toasts use (`0 6px 20px #0005`), while the drag ghost uses (`0 5px 16px #0004`).

**The Overlay Depth Rule.** Reserve elevation for floating feedback and drag affordances; repeated workspace tiles stay flat.

## Shapes

Controls use the small control radius; tiles have square corners, and floating feedback uses the overlay radius. Borders are generally one pixel. Selection artwork uses non-scaling outlines, while shared-edge snap guides use dashed amber strokes. Authored inline SVGs supply action icons, including the toast dismiss control.

## Components

### Buttons

Compact, labelled actions use Field Steel with Divider Steel borders. Primary export and confirmation actions use Action Amber, dark text and semibold weight. Hover uses the recorded hover fill and a Muted Steel border. Keyboard focus is an amber (2px) outline with a (2px) offset. Disabled controls use (.45) opacity. Background/border transitions last (.1s ease-out) only when reduced motion is not requested.

### Inputs and fields

Native inputs and selects share the control shape, field fill and divider border. Supporting labels remain above fields; placeholders explicitly use Muted Steel at full opacity. Fields retain amber carets/focus and Error Peach invalid borders. Color controls pair the native swatch with a labelled monospace hex field; disabled overrides inherit their parent theme.

### Pane and group navigation

Mobile pane tabs share available width and express the chosen pane with an amber border and Active Recess fill. Assembly-group tabs wrap, show a count, accept panel drops and use the same active treatment; their list scrolls when it exceeds its bounded height. They are functional navigation and destinations, not decorative tags.

### Library tiles and order rows

Square thumbnail tiles place the actual filename above its contained artwork. Click adds a panel; drag copies it into an assembly destination. Order rows expose a 1-based numeric position, filename and SVG move/remove actions. Selected rows use Active Recess; dragging reduces opacity and valid destinations show amber edges or outlines.

### Preview and Poster

The preview checkerboard surrounds transparent artwork. Wheel zoom follows the pointer; right-button drag pans; Fit restores the fitted view. View changes do not change export geometry. Posters support free placement, independent panel widths, optional shared-edge snapping and dashed guides. Add column increases the minimum width, and placements farther right or down expand the composed canvas. Poster is not limited to two columns.

### Selection controls

The bottom-right overlay names the edited colour group, lists click/modifier shortcuts, reports the staged count and exposes Confirm selection/Cancel. Escape cancels. Orange target bounds use (.18) fill opacity; yellow staged bounds use (.12), both with non-scaling (1.5px) strokes. Deleting a colour group, confirming member removal or clearing an idle group restores the affected source SVG paints, including distinct text-span colors after other themes.

### Notifications and artwork provenance

Compact bottom-right toasts contain status text, an optional download link and an authored SVG dismiss button. Errors use Error Peach; the status region announces updates politely.

The builder embeds origin metadata in all 31 PNG thumbnails resized from the approved panel PNGs. These are library previews; actual exports compose the editable native SVG panels and retain transparent backgrounds. The sidecar's sample library tile uses inline native panel artwork to remain self-contained.

## Do's and Don'ts

### Do:

- **Do** preserve the approved steel-and-amber world and Oxanium/Inter role split.
- **Do** keep filenames above thumbnails, labelled controls and visible keyboard focus.
- **Do** keep the window-fit workspace and independently scrolling panes.
- **Do** preserve the orange/yellow draft states, explicit confirmation and original-paint restoration.
- **Do** compose exports from native SVGs and preserve thumbnail provenance.

### Don't:

- **Don't** turn preview backgrounds, zoom or selection overlays into exported artwork.
- **Don't** apply floating-overlay shadows to the flat library tiles and pane surfaces.
- **Don't** replace compact utility typography with the guide's larger reading scale.
- **Don't** treat Poster width as a fixed canvas boundary or a two-column limit.
- **Don't** apply this local utility record to unrelated screens or the in-game script.
