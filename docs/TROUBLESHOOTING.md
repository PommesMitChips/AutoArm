# Troubleshooting

## Fixing "Head*; found X"

For a simple arm, name exactly one terminal head block `<arm> - Head - <description>`. For tools, name one reference block per positive enum: `<arm> - Head 1 - Drill`, `<arm> - Head 2 - Welder`. Mounts and merges keep ordinary names. Install the ToolSwap PB for numbered tools and run On on the Arm PB.

## Module pairing

Both scripts must run on the same construct. Empty settings detect the partner by its current global Format and declared arm section; generic PB names are fine. If several possible partners declare the same arm, explicitly set ToolSwapPB in its Arm section or Link.ArmPB in its ToolSwap section. Each arm name belongs to exactly one Arm PB. A ToolSwap PB can declare several arms with different Link.ArmPB overrides.

## Ambiguous rotor base or tool region

Start tools parked on connected facing merges. The named reference, rotor base and head merges must belong to the same connected rigid tool region. Two loose eligible rotor bases need a deliberately configured head identity or a less ambiguous arrangement. Rotors/hinges inside the main arm need no names; the detachable tool coupler must be a supported rotor base carried by the tool. The shared unnamed rotor top stays on the arm.

## Bare arm startup

Install one compatible loose rotor top on the arm end. Remove extra loose parts or surrounding blocks that hide its mounting face. The top can sit directly on a final hinge head. First pickup requires unlimited tool-rotor angle limits; Rotor Lock is restored after verification. No ArmTip offset, camera, setup mount or teaching is required.

## Wrong format or stale tool data

Arm PB data requires [global] Format=7; ToolSwap requires [global] Format=2. Delete older PB Custom Data and allow regeneration, then restore desired preferences. There is no migration. Each automatically configured head stores AutoArm Tool Format=1. If that section has an unsupported format, delete only that section and scan the tool while parked; preserve unrelated Custom Data.

## Shared structure mismatch

Global Actuators settings require the configured joint kinds, member counts and split/rejoin arrangement. Lengths and world mounting geometry may differ. Fix the nonconforming arm or give it local Structure and Actuators overrides. Coupling/downstream tool joints are excluded from this arm-body template. An invalid arm stays OFF without stopping an unrelated valid arm.

## Motion stops or moves slowly

Stop cancels only the target arm; StopAll stops all arms hosted by that PB. Select chooses cockpit input. Automatic movement for an unselected arm continues independently. Resolve reported topology/attachment/communication faults, then run On for that arm. Expected ToolSwap changes rebuild and resume automatically.

If a previously mounted known tool is manually parked and fully detached, an intact arm can cancel the old operation, rediscover its bare rotor tip and resume a fresh ON hold. Pending/foreign attachments, missing parts, broken upstream joints and failed discovery remain stopped. Explicit Stop/Off/SwapCancel or module stop authority inhibits this recovery; run On to request control again. Both PBs must use the current scripts to carry stop intent through the handoff.

An arm can be ON while another arm owns cockpit input. Run `Select <arm name>` on its Arm PB to choose it. The status reports when cockpit input is inactive at that PB. An initial configuration error followed by a successful On now establishes the default selection if no owner exists.

`PositionDamping` and `OrientationDamping` default to 0 and can be set globally or per arm. Explicit older values remain unchanged; set both to zero to use the earlier undamped response. The final insertion target gradually reduces forward demand as alignment worsens and holds current progress when badly misaligned. It does not retreat to the line start. If movement still oscillates, record the position/angular errors and active speed, acceleration and damping settings; native SE joint flex and contact are not simulated by the test harness.

For parking, `Tools.PositionTolerance` defaults to 0.01 metres and `Tools.AngleTolerance` to 1 degree. Explicit older values of 0.005 and 0.2 still apply until edited. These settings belong in the ToolSwap PB's `[global]` or arm section. Rotor pickup retains tighter alignment. A briefly unavailable source attachment after the support merges connect waits with zero drive output, for at most two seconds. A wrong part, foreign owner or lost support still cancels the operation.

Travel uses the arm's HeadSpeed/HeadTurnSpeed. ToolSwap's slower defaults apply to final docking/insertion and withdrawal. Joint speed, acceleration, limits and actual physics still bound achievable movement. Multiple arms share one PB instruction budget; distribute larger arms over more PBs if the reserve guard stops them.

## Tool return orientation

Keep tools merged to their intended stands for initial scanning. The actual parked orientation is saved on the named reference block. Returning to that unchanged rack reuses it; a different rack or unobserved parked pose may require the first geometric alignment. Choose clear routes: collision avoidance is not included.
