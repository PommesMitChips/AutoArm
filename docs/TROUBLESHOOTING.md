# Troubleshooting

## “Head*; found X”

With tools disabled, exactly one terminal block must start with the arm's head prefix. For `Arm 1`, names such as `Arm 1 - Head 1 - Drill` and `Arm 1 - Head 1 - HeadMerge` both match `Arm 1 - Head`. Keep that prefix on only the reference block for a simple arm.

For a tool setup, enable `[Tools]`, configure the exact end-mount name and complete marker names in `Heads`, and run `Reload`. Edit existing sections rather than adding duplicate sections. `Check` does not load configuration edits.

## Unlearned tools

Mount each new tool once on the configured end mount and run `ToolScan`. An unlearned parked profile does not prevent manual operation of the learned mounted tool, but it cannot be selected for automatic pickup. Tops remain unnamed.

Keep support connected until the real rotor/hinge mount is attached. After a first setup mount and scan, disable only the currently mounted tool's head-side merges, wait for separation and scan again.

## “Cached top outside tool”

Check that the reference block, top and head-side merges belong to the same connected rigid tool assembly. Changing the assembly, replacing its top/reference or moving to another PB can require another setup mount.

## “Top attached to …” or “Tool attached elsewhere”

The tool's top is still attached to another base. `[Tools] Mount` must identify the actuated end base on this arm. Detach a parked tool's top from any other base while keeping it supported on its stand.

## Missing or ambiguous names

Names in `Heads`, `Mount` and explicit merge lists must match the whole name, including spaces and capitalization. `Arm 1 - Head 1` does not identify a block actually named `Arm 1 - Head 1 - Drill`. Remove profiles for absent tools and keep `ToolNN` sections in the order of the `Heads` list.

## Automatic merge names

Explicit merge lists work when their automatic scan flags are false. For automatic stand scanning, an empty `StandPrefix` derives from the entire marker name. Marker `Arm 1 - Head 1 - Drill` implies stand prefix `Arm 1 - Head 1 - Drill - StandMerge `. Alternatively, name the marker `Arm 1 - Head 1` and number its stand merges `Arm 1 - Head 1 - StandMerge 1`, etc.

Automatic generic parking uses numbered names such as `Arm 1 - ParkMerge 1`. An explicitly listed unnumbered park merge works with `Park`, but `Park 1` requires the exact numbered name. Per-tool stands are used for swaps; they are not automatically generic park destinations.

## Configuration format

If the loader reports the wrong format, save the preferences you want to retain, delete Custom Data and run `Reload`. Current configuration uses Format 3. When installing into another PB, clear a copied `Actuators` table so the new PB can generate its own rows.

## Interrupted swaps

`Stop`, `Off`, `SwapCancel` or pilot input stops automatic movement while retaining support. Inspect the connections and run `ToolScan` before continuing. Automatic completion stays OFF; run `On` for manual control afterward.
