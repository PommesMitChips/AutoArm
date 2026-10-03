# ToolSwap Workshop assets

`branding/` contains the AutoArm square thumbnail, ToolSwap title image and
ToolSwap square thumbnail, based on the supplied AutoArm artwork. Both thumbnails
are exported at 512x512px. The title image is 1672x941px. These retain
the reference's pale technical background. They were made with built-in
imagegen; the original prompt set is in `generation-prompts.json` and final
perspective corrections are in `correction-prompts.json`. Tool sockets face
left toward the viewer; their housings and working ends recede right away.

`panels/` contains eight transparent PNG panels: an introduction and seven setup
steps. Text and artwork are baked into each panel, so the layout is preserved in
the Workshop. Every panel is 1240px wide, with a transparent canvas. The preview
shows the exports against the dark Workshop background.

The geometry diagrams reuse the original AutoArm SVG models, with a shared
scene-level surface renderer for correct occlusion. The rotor head
stays on the arm, and each tool carries a rotor base. Each tool is supported by
a vertical merge pair directly beneath its central housing. Images 1 and 3
have working head, tool housing and rotor base across the top; only the two
middle-column merge blocks occupy the next two rows. Image 1's tools face right
and up, away from the viewer, with sockets facing the opposite foreground direction.
Image 3 retains the original arm presentation. The schematic proportions illustrate the
arrangement; they are not an in-game blueprint or a tested motion path.

For the example, one reference is named `Arm 1 - Head 1 - Drill` and another
`Arm 1 - Head 2 - Welder`. The rotor bases are named `Arm 1 - Mount 1` and
`Arm 1 - Mount 2` for identification. Those mount names are optional: the current
script discovers the bases from the tool regions. Each tool must have one
eligible rotor base and one numbered reference marker.

Pairing uses `ToolSwapPB=ToolSwap PB` in the AutoArm PB's existing `[Arm 1]`
section. The reciprocal ToolSwap setting is `Link.ArmPB=AutoArm PB`, also in its
arm section. Keep existing generated settings and use the actual PB and arm
names. Copyable snippets are included as `autoarm-pairing.ini` and
`toolswap-pairing.ini`. ToolSwap has a specialized pairing field; it is not a
service entry in `Peers`.

The guide was checked against the local README, ToolSwap source and configuration
examples; their hashes are recorded in `manifest.json`. No script, live game or
Steam Workshop item was modified or published.

Rebuild native artwork with `python build.py --art`, then
`node render_artwork.mjs /path/to/sharp`, then `python build.py`. The typography
and assembly helpers come from the parent guide builder and existing SVG library.
