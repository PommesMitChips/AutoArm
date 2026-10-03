# AutoArm and ToolSwap command panels

Fourteen transparent PNG panels, typeset in the same Inter/Oxanium, steel and
amber design as the Getting Started panels. Every image is 1240px wide. There is
one shared command-format panel, eleven AutoArm panels and two ToolSwap panels.

The list covers public Run commands in the current local 5.1.0
scripts: aliases, fixed numeric shortcuts, PivotProbe subcommands and the tool
commands forwarded by AutoArm. Recognized diagnostic no-ops and rejected legacy
names are clearly identified. The control wrapper and its short alias are omitted
at the user's request. IGC protocol messages are not public Run commands.

`commands.txt` is the copyable text reference. `catalog.json` is the editable
panel copy. `preview.html` shows the PNGs on the Workshop background. The PNG
canvases themselves are transparent, including the text panels.

The builder checks the command inventory against the source dispatchers and
records their hashes in `manifest.json`; it checks against the selected commands,
with explicit user-requested omissions recorded in the catalog. It fails for an
unexpected missing or unsupported verb. Descriptions and argument details were
independently audited against the source and existing README by the command
inventory agent.

Run `python build.py` with Pillow available to regenerate the PNGs and ZIP.
Fonts and layout helpers are reused from the parent Getting Started builder.
