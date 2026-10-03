# Editable SVG panels

All 31 guide/reference panels have been reassembled as SVGs: 9 AutoArm guide
panels, 8 ToolSwap guide panels and 14 command panels. Headings, paragraphs,
commands, block names, terminal labels and settings remain editable text. The
illustrations contain native vector geometry, not embedded PNGs. All backgrounds
remain transparent.

Panel separator rules are separate from the panels, including the dividers
between command entries. Use `common/separator.svg` wherever you want a divider.
It has a transparent 1240 × 48 canvas and the original editable line colour.

Open a panel in Inkscape, Illustrator or another SVG editor. Use the Text tool to
edit wording, or select its named artwork group to move/resize an illustration.
Text has explicit line breaks to preserve the approved layout; adjust those
breaks after changing copy length. Shapes and field/button fills are separate
editable objects. Illustration surface order is retained for correct overlap.

Inter and Oxanium fonts and their open licenses are included in `fonts/`.
Install them if your SVG editor substitutes fonts. Browser previews also have
embedded copies of these fonts. Code uses the existing Windows Consolas font,
with monospace fallbacks; Consolas is not redistributed. The light text is meant
for a dark canvas, so use `preview.html` or a dark editor canvas while editing.

The latest approved edits are retained: image 1's tools point right/up/away,
image 3's arm is unchanged, the two vertical merges sit under each tool housing,
and the command control wrapper remains removed. Raster title/thumbnail branding
is separate from these guide/reference panels.

To regenerate from the original panel layout sources, run `python tools/asset-builders/workshop/svg-panels/build.py`
from the repository root. The standalone
ZIP contains the editable panels, fonts and preview; regeneration uses the
layout sources in the repository.
Use `python tools/asset-builders/workshop/svg-panels/build.py --strip-separators` to remove separator rules from existing
SVGs without regenerating their text or artwork. The assembler's element keys
are retained so existing colour selections keep targeting the same elements.
Regeneration overwrites these generated SVG copies; keep manually edited copies
elsewhere if you want to retain those edits. The existing PNGs and copy sources
are preserved.
