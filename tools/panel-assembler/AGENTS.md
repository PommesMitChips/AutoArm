# panel-assembler

Offline panel library and assembly editor; runtime output belongs in dist.

## Files

- `app.js`: Panel library, grouping, colour-selection, preview and export interactions.
- `build.py`: Build/package the tool or graphic library using repository-relative data paths.
- `core.js`: SVG parsing, stable element keys, themes, placement, templates and ZIP composition.
- `core.test.cjs`: Meaningful core regression cases.
- `DESIGN.md`: Implemented design tokens and interaction rules.
- `index.html`: Editable UI markup and bundle placeholders.
- `README.md`: Tool use and development instructions.
- `style.css`: Window-fit layout, control styling and responsive panes.

Do not overwrite user-edited SVGs or copy. Regenerate through tools/Build.ps1 or
the named Python generator. Keep tool code outside docs/assets and retain
source provenance for generated artwork.
