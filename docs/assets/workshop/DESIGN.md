# Workshop PNG guide

The current requested deliverable is a series of transparent PNG panels based
on the original illustrated guide. Each panel contains both its wording and
artwork; Steam no longer supplies the text layout.

- **THESIS:** Build the arm, name its base and head, load AutoArm and take control.
- **OWN-WORLD:** Preserve the original Inter/Oxanium typography, steel and amber
  mechanical palette, numbered editorial rows, terminal mockups and control keys.
  Text uses light steel for the Workshop background. The PNG canvas stays clear.
- **STORY:** The user's edited `getting-started.bbcode.txt` is the sole prose
  source, read without modification: title, introduction and eight steps. The
  removed ninth step and removed instructions stay absent. Step 7 uses Browse
  Scripts and selecting the script, as edited by the user.
- **FIRST VIEWPORT:** The original two-line title and the user's introduction.
- **FORM:** Nine separate PNGs: one title panel and eight step panels. Ordinary
  steps retain text beside artwork. Step 3 retains the labeled supported arm on
  the left and the two unsupported examples on the right. Step 6 retains the
  classical backward-lower/forward-upper pose. Step 8 uses the original keycap
  treatment for the edited control pairs.

Panels are composed at 620px logical width and exported at 2x as RGBA PNGs.
Their corners and unused canvas have alpha zero. UI controls keep their own
fills, with no full-canvas rectangle. Diagram labels are enlarged for the narrow
column. `AutoArm-Getting-Started-PNG.zip` contains only the nine finished PNGs.

`build_panels.py` records the edited copy hash and all rendered text blocks in
`panels-manifest.json`. `panels-preview.html` shows the actual exports against
the dark Workshop background. The original HTML/SVG guide is preserved. The
earlier native BBCode/image export remains available separately.
