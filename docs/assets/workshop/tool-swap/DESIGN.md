# ToolSwap Workshop graphics

The existing AutoArm guide supplies the panel layout, Inter/Oxanium typography,
steel/amber palette and schematic SVG parts. The user-supplied AutoArm illustration
supplies the separate branding images' robot pose, contour style, pale technical
backdrop and heavy wordmark.

- **THESIS:** Park the tools, put the correct rotor half on each side, name the
  references and mounts, pair the programmable blocks, then swap through AutoArm.
- **OWN-WORLD:** Keep the approved guide's steel and amber geometry, restrained
  leaders, readable numbered rows and game-like terminal fields. Branding stays
  faithful to the supplied pale-background illustration.
- **STORY:** Eight setup panels: intro, stands, rotor head on the arm, rotor bases
  on the tools, reference/mount naming, second PB, Custom Data pairing and commands.
- **FIRST VIEWPORT:** Getting Started with ToolSwap, followed by the parked tools.
- **FORM:** 620px logical width, 2x PNG exports, transparent guide canvases. Naming
  and command panels use the existing two-column rhythm; wide mechanical diagrams
  and paired configuration snippets use the full measure.

Every tool has one numbered Head reference and one rotor base. Mount names are
shown as optional identification. Pairing uses ToolSwapPB in the AutoArm arm section
and Link.ArmPB in the ToolSwap arm section, with Tools.Enabled=true. Names and keys
were checked against local source and configuration examples. Peers is a distinct
service interface, so no ToolSwap row is invented for it.

Both thumbnails are 512px square; the wide ToolSwap title preserves its generated
1672x941 composition. Generated full-resolution masters and all prompt text stay
in the workspace. Final published assets are saved in branding/ and panels/.
The branding sockets face left into the foreground, and their bits recede right
into the background on the same axes. Guide images 1 and 3 use a three-column
top row (working head, tool housing, rotor base) with two vertically mating merge
blocks in the middle column below, leaving both side columns as air. The tools
in image 1 point right and up, away from the viewer; the sockets face the opposite
foreground direction. Image 3 preserves the original arm geometry and
camera framing. The mechanical diagrams use exact modeled
transforms, mating merge-face positions and a shared scene-level surface renderer
for proper occlusion. They remain schematic artwork rather than an in-game tested
blueprint. Final branding corrections are recorded in correction-prompts.json.
