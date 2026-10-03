# Getting Started artwork

The SVGs in this folder accompany `docs/getting-started.html` and `docs/GETTING_STARTED.md`.

- `rotor-on-grid.svg`: the first actuator, in isometric view.
- `arm-front.svg`: the complete requested arm, with labels in build order from bottom to top.
- `arm-isometric-pb.svg`: the same arm in a classical shoulder–elbow–wrist pose with a downward-facing drill, plus a programmable block and control seat on a connected foundation.
- `unsupported-misaligned.svg` and `unsupported-hydraulic.svg`: offset parallel-stage and hydraulic-linkage examples.
- `name-base-terminal.svg` and `name-head-terminal.svg`: terminal naming mockups based on the supplied reference.
- `run-terminal.svg`: Edit, Argument and Run mockup based on the supplied reference.

The assembly uses the established block library and common schematic scale. It contains four rotors, six hinges, three pistons, two three-block crossbars and a drill. The paired lower hinges have a shared axis. In the serial chain, the second hinge of the final pair is rotated 90° around the arm's upright axis so its pivot axis is perpendicular to the preceding hinge.

`manifest.json` records the component positions, order and rotations, including the folded pose. The paired lower hinge heads rotate backward together; their two pistons and shared crossbar follow that motion. The upper serial section folds forward, with the drill pointing downward. Each following component is placed at the transformed head anchor. These diagrams are explanatory artwork, not a dimensionally exact blueprint or an in-game tested build.

Install the guide builder's dependency with `python -m pip install fonttools`. Run `python docs/assets/blocks/build.py` first, then `python docs/assets/models/autoarm/build.py` from the repository root. The guide generator uses `fontTools` to outline the bundled Oxanium font in the terminal mockups. Those SVGs remain editable and render without installed fonts. The HTML page uses self-hosted Inter and Oxanium from the official Google Fonts repository; their SIL Open Font License files are included in `assets/fonts`.

User references: `codex-clipboard-01627974-45fb-48a1-a885-8da0f1b0d10a.png` and `codex-clipboard-faf1a543-7121-490d-a5a8-9a1c595642c3.png`. The mockups reproduce the relevant controls as vectors rather than embedding the screenshots.

Use `python docs/assets/models/autoarm/build.py --diagrams-only` to rebuild the geometry and bundle while preserving the existing terminal mockups; this mode does not need fontTools.
