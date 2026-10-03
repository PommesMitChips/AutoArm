# AutoArm panel assembler

Open **AutoArm-Panel-Assembler.html** in Chrome or Edge. It is a standalone
offline utility: the 31 SVG panels, reusable separator SVG and Inter/Oxanium
fonts are already inside.
No installation, server, or internet connection is required.

1. Load a built-in template or create an assembly group. The library displays
   filenames above thumbnails. Hover a truncated filename to see its full name.
2. Drag a library tile onto an assembly-group button or its panel order list.
   Click a tile to add it to the current group. Drag order rows to reorder them,
   or enter a **1-based position** in a row's number field and press Enter.
   Panels have no built-in separator rules. Add `separator.svg` from **Layout
   elements** wherever you want a divider; its line colour follows the template
   line controls and can also be overridden individually.
3. Choose **Vertical stack**, **Horizontal row**, or **Poster**. Poster permits
   free placement, independent panel widths and optional shared-edge snapping.
   **Add column** widens its minimum canvas; dragging panels farther right also
   expands the exported canvas. Posters can contain more than two columns.
4. Scroll the wheel over the preview to zoom around the mouse pointer. Hold the
   **right mouse button and drag** to pan. Zoom and pan affect the preview only.
5. Expand **Template**, name a colour group and choose **Create**. Its **Edit
   colour group** button toggles selection editing. Click adds an element;
   Ctrl-click removes it; Shift-click adds matching colours; Ctrl-Shift-click
   removes matching colours. Matching targets highlight orange before a click.
   The staged selection stays yellow and can be changed before **Confirm
   selection**. **Cancel** or Escape discards that draft. Only confirmation
   changes group membership. The controls appear in the bottom-right overlay.
6. Set the group's colour. Deleting a group, clearing its selection, or confirming removal of members,
   restores those elements' source SVG colours even when a white text theme is
   active. Mixed-colour text spans preserve their distinct source colours.
7. Export the current group, all groups joined vertically as one image, or
   separate group files in a ZIP. Choose PNG or editable assembled SVG.
8. Save the configuration as a JSON template. Imported SVGs, colour memberships
   and Poster positions travel with the configuration. Confirm or cancel a
   colour selection before saving, loading or exporting.

The checkerboard/background selector affects only the preview. PNG and SVG
exports remain transparent. A width of 620 at 2× exports a 1240-pixel-wide PNG.
All-in-one export keeps each group's chosen width, centres narrower groups,
and adds 24 pixels between groups. Group ZIPs retain their separate sizes and
filenames; duplicate filenames receive numeric suffixes.

Colours resolve in this order: original SVG → template → group → panel →
individual element → named colour group. Disabled group/panel overrides inherit
their parent. An element's `"original"` paint override restores its source
paint after the theme layers; deletion uses this to recover original colours.
Setting a colour to `null` in a JSON theme explicitly keeps the original colour.
Original text, geometry, surface order and source SVG files are not rewritten.
This utility changes assembly and colours; edit text wording in your SVG editor
and use **Import SVG files** to add that version.

The browser remembers the latest session where local storage is available.
**Save template** is the portable way to keep your work. Loading another template
replaces the current arrangement; save your template first if you need it later.

Very large PNGs are limited to 32,760 pixels on either side and 64 million
pixels in total. Reduce width/scale or export separate groups when the utility
reports this limit. SVG export has no raster dimension limit.

## Template example

```json
{
  "version": 1,
  "name": "My workshop guide",
  "theme": {"text": "#ffffff", "line": "#849da9", "lineScope": "rules"},
  "scale": 2,
  "groups": [
    {
      "id": "setup",
      "name": "Setup",
      "filename": "setup",
      "layout": "vertical",
      "width": 620,
      "gap": 20,
      "padding": 0,
      "panels": [
        {"asset": "autoarm-guide/01-place-first-actuator.svg"},
        {
          "asset": "autoarm-guide/02-name-base.svg",
          "theme": {"text": "#e9b34c"},
          "elements": {"node-0": {"fill": "#ffffff"}}
        }
      ]
    }
  ]
}
```

Element keys appear in the utility's element menu for lines. Text keys refer to
the stable drawable order in that particular SVG. Saved templates record them
automatically. Importing a structurally different SVG creates a new asset, so
an existing panel's element keys do not silently change.

To update the bundled panel library after changing the repository SVGs, run
`python tools/panel-assembler/build.py` from the AArmOS repository.
The source UI is `index.html`, with `style.css`, `core.js` and `app.js`; the builder produces
the standalone HTML and ZIP in `tools/panel-assembler/dist/`.

Run the assembly/template/ZIP tests with `node --test core.test.cjs` in this
folder. Browser PNG/font and transparency checks are separate from those tests.
