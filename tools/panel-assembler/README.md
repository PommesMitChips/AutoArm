# Panel assembler

Open **AutoArm-Panel-Assembler.html** in Chrome or Edge. The panel library is included, and the tool works offline.

## Arrange panels

1. Load a template or add an assembly group with **+**.
2. Drag panels from the library into a group. Clicking a panel adds it to the
   selected group.
3. Drag rows in **Panel order** to reorder them, or enter a position in the
   number field and press Enter.
4. Choose **Vertical stack**, **Horizontal row** or **Poster** and set the width,
   spacing and padding.

In Poster mode, drag panels in the preview to position them. Use **Add column**
to widen the poster, and toggle **Snap to shared edges** as needed.

Scroll over the preview to zoom. Hold the right mouse button and drag to pan.
Add `separator.svg` from **Layout elements** wherever you want a divider.

## Change colours

Expand **Template** to set text and line colours. Group and panel overrides let
you change individual parts of the assembly.

For a custom selection, enter a colour group name and click **Create**, then
**Edit colour group**. Select elements in the preview:

| Control | Action |
| --- | --- |
| Click | Add an element |
| Ctrl + click | Remove an element |
| Shift + click | Add all elements with the same colour |
| Ctrl + Shift + click | Remove all elements with the same colour |

Orange highlights show the elements a click will affect. Yellow highlights show
your current selection. Adjust it as needed, then click **Confirm selection**.
**Cancel** or Escape discards the selection. Set the colour using the group's
colour control. Removing a colour group restores the elements' original colours.

## Export and save

Choose **Current group**, **All groups as one image** or **Each group in a ZIP**,
then click **Export PNG** or **SVG**. Exports have transparent backgrounds.
Scale controls the PNG resolution: a width of 620 at 2× produces a 1240-pixel-wide
image.

Use **Save template** to keep your arrangement and colours as a JSON file.
**Import template** loads it again. To change a panel's wording, edit its SVG in
a vector editor and add it with **Import SVG files**.

## Development

Rebuild the bundled tool after editing its code or panel library:

```powershell
./tools/Build.ps1 -Scripts PanelAssembler
```

Run from the repository root with Python and Pillow installed. The HTML and ZIP
are written to `tools/panel-assembler/dist/`.

```powershell
node --test tools/panel-assembler/core.test.cjs
```
