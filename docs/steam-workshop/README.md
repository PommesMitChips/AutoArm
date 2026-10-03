# AutoArm — Steam Workshop version

This package contains the current nine-step guide as Steam BBCode and nine PNG illustrations adapted for dark backgrounds. The classical pose, endpoint names and two remaining unsupported examples match the local guide.

## Put it on your Workshop page

1. Upload the nine files in `png/` to your chosen image host, preserving their PNG format and transparency.
2. Get a direct HTTPS image URL for each file.
3. In `getting-started.bbcode.txt`, replace each `{{IMAGE_...}}` placeholder with its corresponding URL. The filenames and placeholders are mapped below and in `manifest.json`.
4. Paste the completed text into the Workshop item's description.

The package is a publishing draft. Images have not been uploaded and the URL placeholders must be replaced before posting. There is no account or hosting destination assumed by this export.

If convenient, fill `image-urls.json` and run `python resolve_urls.py`. It writes `getting-started.ready.bbcode.txt` only when every image has a valid HTTPS URL. You can also replace the placeholders directly in any text editor.

| Placeholder | PNG |
| --- | --- |
| `IMAGE_01_ROTOR_ON_GRID` | `01-rotor-on-grid.png` |
| `IMAGE_02_NAME_BASE` | `02-name-base.png` |
| `IMAGE_03_ARM_FRONT` | `03-arm-front.png` |
| `IMAGE_03A_MISALIGNED_STAGES` | `03a-misaligned-stages.png` |
| `IMAGE_03B_HYDRAULIC_LINKAGE` | `03b-hydraulic-linkage.png` |
| `IMAGE_04_NAME_HEAD` | `04-name-head.png` |
| `IMAGE_05_CONTROLLER` | `05-controller.png` |
| `IMAGE_06_ARM_AND_PROGRAMMABLE_BLOCK` | `06-arm-and-programmable-block.png` |
| `IMAGE_07_RUN_ON` | `07-run-on.png` |

## Preview and artwork

Open `preview.html` to inspect the guide against dark Steam-style surfaces. The local preview uses the real exported PNGs; it is an approximation of Workshop layout, not Steam's own rendering engine.

All publishing images are RGBA PNGs with transparent canvas and corners. Mechanical surfaces and real terminal controls remain painted. The terminal mockups have no full-canvas background panel. Labels and leaders are light; the steel palette and amber accents are adjusted for dark backgrounds. The image manifest records dimensions and alpha-channel verification results.

The BBCode uses native heading, bold, list and link tags rather than custom text-color or layout tags. The image placeholders use `[img]URL[/img]`. The controls table is adapted to a simple list for the narrow Workshop description column.

Formatting references: [Valve's Workshop formatting help](https://steamcommunity.com/comment/WorkshopItem/formattinghelp) and the [Workshop image-embedding demonstration](https://steamcommunity.com/sharedfiles/filedetails/?id=286731518). The source guide is `docs/GETTING_STARTED.md`; the diagrams derive from the existing independently drawn AutoArm SVGs.

## Rebuild inside the AutoArm repository

Run `python docs/steam-workshop/build.py`, then `node docs/steam-workshop/render.mjs` with `sharp` available, then `python docs/steam-workshop/build.py --pack-only`. The PNG package does not need fonts or an SVG renderer to use. The themed intermediate SVGs stay in `.render/` and are not part of the publishing ZIP.
