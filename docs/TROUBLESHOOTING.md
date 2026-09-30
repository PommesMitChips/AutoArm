# Troubleshooting

## “Head*; found X”

With tools disabled, exactly one terminal block must start with the arm's head prefix. For `Arm 1`, names such as `Arm 1 - Head 1 - Drill` and `Arm 1 - Head 1 - HeadMerge` both match `Arm 1 - Head`. Keep that prefix on only the reference block for a simple arm.

For a tool setup, enable `[Tools]`, configure each tool-side rotor base in `[ToolNN] Mount`, and complete marker names in `Heads`. Run `Reload`; `Check` does not load configuration edits.

## Missing or ambiguous names

Names must match the whole name, including spaces and capitalization. `Arm 1 - Head 1` does not identify a block actually named `Arm 1 - Head 1 - Drill`. Remove profiles for absent tools and keep `ToolNN` sections in the order of `Heads`.

## Tool mount and arm tip

The rotor base belongs on the tool; one unnamed rotor part stays on the moving arm. There is no arm-side Mount name or ArmTip offset. The part can sit directly on the last hinge's moving head. AutoArm follows the named Base, finds that part, and uses it as the bare movement focus. Each tool's named base, marker and head merges remain on its tool assembly.

`No loose arm rotor part` means no candidate was found on reachable moving arm grids. Check the Base attachment and the part's rigid connection to the arm. Multiple loose parts or a part surrounded on several possible mounting faces are ambiguous; remove spare parts or make the mounting arrangement clear.

First automatic acquisition needs unlimited lower/upper angle limits on the selected tool rotor. Rotor Lock is supported and restored after verification. If cancelled while pending, keep support and use ToolScan; it cancels a confirmed bare pending request before restoring the lock. No tool merge is released for an unverified actual part.

`Tool attached elsewhere` means a configured tool base is attached to a different top. Keep that tool supported and remove its unintended attachment before scanning. Do not move an arm still attached to a merged tool.

## Automatic merge names

Explicit merge lists work when their automatic scan flags are false. For automatic stand scanning, an empty `StandPrefix` derives from the entire marker name. Marker `Arm 1 - Head 1 - Drill` implies stand prefix `Arm 1 - Head 1 - Drill - StandMerge `. Alternatively, supply the explicit list shown in the examples.

Automatic generic parking uses numbered names such as `Arm 1 - ParkMerge 1`. An explicitly listed unnumbered park merge works with `Park`, but `Park 1` requires the exact numbered name. Per-tool stands are used for swaps; they are not automatically generic park destinations.

## Configuration format

Current configuration uses Format 5. If the loader reports the wrong format, save the preferences you want to retain, delete Custom Data and run `Reload`. When installing into another PB, clear a copied `Actuators` table so the new PB can generate its own rows.

## Interrupted swaps

`Stop`, `Off`, `SwapCancel` or pilot input stops automatic movement while retaining support. Inspect the connections and run `ToolScan` before continuing. An explicit scan clears a pending failed attachment only when that base is actually bare. Automatic completion stays OFF; run `On` for manual control afterward.