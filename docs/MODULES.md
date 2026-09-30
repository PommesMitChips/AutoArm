# PB modules and paths

AutoArm owns arm topology, joint state, held targets, Home, the task-space solver and all actuator-velocity writes. ToolSwap owns tool profiles, docking geometry, support/attachment sequencing and its recovery journal. Shared math, rotor-part descriptors and transport live in reusable source fragments. Neither module calls the other recursively.

## Installation boundary

Arm Custom Data is Format 6. `Modules.ToolSwapPB` is empty for a standalone arm, or the exact same-construct ToolSwap PB name. ToolSwap Custom Data is Format 1; `Link.ArmPB` is the exact arm PB name. Both scripts use the same `ArmName`. Formats are strict; there is no configuration migration.

ToolSwap sends endpoint/reference/focus descriptors during stopped topology changes. AutoArm resolves the actual blocks, discovers the mechanical corridor, retains weights by physical member identity and performs stopped setup. The authoritative groups and head identity are returned to ToolSwap. ToolSwap never writes rotor/piston velocities, including during cancellation. It changes only tool-side coupling state, rotor lock and the selected tool's support merges.

## Transport

Messages are unicast IGC strings tagged `AutoArm/4`, encoded as INI. Both peers check the configured PB identity, same construct, protocol version and arm name. The `[Link]` envelope contains `Version=4`, `Arm`, sender `Epoch`, expected receiver `Remote`, `Generation`, increasing `Sequence` and `Operation`. Epochs survive recompilation in each PB's Storage. Stop revokes the generation so old requests cannot restart motion. There is one authorized movement lease. Both PBs must use the paired current scripts.

| Operation | Direction | Meaning |
| --- | --- | --- |
| `COMMAND` | Arm → ToolSwap | Forward `On`, `Reload`, tool/park commands or cancellation |
| `HELLO` | ToolSwap → Arm | Discover the current session; acquire a lease only with matching epoch/generation |
| `BUILD` | ToolSwap → Arm | Apply endpoint/equipment descriptors and rebuild stopped topology |
| `TRANSITION` | ToolSwap → Arm | Confirm a stopped, expected merge/mount boundary; pause stale topology until BUILD settles |
| `PATH` | ToolSwap → Arm | Submit the entire ordered pose list |
| `STOP` / `PAUSE` | Lease owner → Arm | Cancel the current path and stop; a new path request is required to continue |
| `RESUME` | ToolSwap → Arm | Return to normal control after verified mounting/setup |
| `RELEASE` | ToolSwap → Arm | Release the lease while remaining OFF |
| `ABORT` | ToolSwap → Arm | Report a failed/canceled operation and revoke movement without requiring endpoint data |
| `PING` | ToolSwap → Arm | Renew liveness and report permission for manual control |
| `INFO` | ToolSwap → Arm | Return requested equipment information without changing a path |
| `STATE` | Arm → ToolSwap | Acknowledged request, readiness, stopped/settled state, error and progress |

Messages are capped at 16,000 characters and processed in bounded batches with an instruction reserve. During linked manual control or a lease, missing valid messages for more than 0.5 seconds stops AutoArm. ToolSwap also stops sequencing on lost replies. Terminal/IGC callbacks process requests; only positive timed observations advance motion or settle topology.

## Path request

`PATH` contains `Waypoints`, `Anchor`, `Move`, `Turn`, `PosTol` and `AngleTol`. A row has nine finite numbers:

```text
x y z fx fy fz ux uy uz | x y z fx fy fz ux uy uz
```

`Anchor=0` means world space. Otherwise it is the EntityId of one live terminal block on the same construct, and every row is local to that block. AutoArm recomputes the anchor transform each timed step; moving stands do not leave stale world-space targets. Forward/up define orientation and must form a valid frame. The whole list (1–32 rows) is validated before execution. Invalid requests stop without executing a partial prefix.

Optional per-waypoint `Moves`, `Turns` and `Lines` use `|`-separated rows of equal count. A speed of zero selects the arm's configured HeadSpeed/HeadTurnSpeed. ToolSwap sends inherited speeds for travel and its explicit slower speeds for insertion/withdrawal. Autonomous task-speed caps are independent of the manual tracking-correction caps, while joint speed, acceleration, travel and synchronization bounds remain in force. A linear waypoint captures its start in the anchor frame, aligns before translating, holds the final orientation and tracks a lookahead point on the Cartesian segment. Completion tests distance to the actual endpoint, not the lookahead reference.

AutoArm advances only after position/orientation tolerances and measured low velocity hold over two positive observations. The existing solver, travel bounds, acceleration bounds, synchronization checks and instruction budget remain authoritative. A waypoint has a deadline derived from initial distance/angle and effective correction caps, with a 30-second allowance.

`STATE` reports `Ack`, `MoveAck`, `PathId`, `Completed`, `Count`, `Ready`, `Stopped`, `Settled`, `Error`, pilot/input settings, authoritative head identity and actuator groups. `Completed` counts reached waypoints. Completion requires the matching path identity; a stale settled reply cannot satisfy a new path. Final stopped output is confirmed before ToolSwap performs attachment mutations. Completion of movement is separate from completion of attachment.

ToolSwap submits approach and insertion together. For example, `ApproachDistance=2.5` creates a first waypoint 2.5 metres outward from the mating pose, followed by the mating pose. Once the full path finishes, ToolSwap verifies support/identity/pose, changes attachment and asks AutoArm to rebuild and resume. Retreat is a one-waypoint path. Parking stays OFF; successful mounting resumes control at the actual current pose.

Before enabling support, attaching/detaching a mount, releasing merges or canceling a pending native attachment, ToolSwap declares and waits for TRANSITION acknowledgement. AutoArm stops commands and blocks movement on stale topology while the coordinator observes native separation/attachment. BUILD rewalks the actual arm and settles it; RESUME activates normal control on a fresh timed update, after equipment permission is valid. Source head merges are disabled during parking travel so native merge snap cannot close the chain before stopped docking. Unexpected upstream connection changes still cancel through the coordinator's identity checks and the arm's active topology validation.

ToolSwap automatically stores parked marker pose relative to a real stand anchor plus merge-member relative frames in its scoped Storage. Fully connected parked profiles are captured during scans and verified support boundaries. Returning to the same rack uses that pose if marker/port identities and rigid layouts match. Grid IDs are not used as parked-tool identity. This preserves the user's rest orientation despite visually symmetric merge faces; there is no manual teaching requirement. An unobserved initial park or a different rack uses geometric matching.

Other PB automation can also invoke the arm's public `Path` Run argument with world-space rows. ToolSwap uses the acknowledged IGC form because it needs progress and a stopped-output fence before changing equipment.

## Additional direct peers

`[Modules] Peers` authorizes up to 16 additional same-construct PBs by exact name, independently of `ToolSwapPB`. Each row is `PB name | Role`. The optional setting is shown in [Modules.ini](../examples/Modules.ini). Arm Format 6 remains valid without it. Invalid roles, duplicate/self/missing/nonfunctional PBs and reuse of the ToolSwap PB as a service peer refuse setup. Apply changes with `On` or `Reload`.

| Role | Authority |
| --- | --- |
| `Observe` | Read readiness, pose, defaults, path progress and current motion owner |
| `Plan` | Submit a complete path while the arm is ON, ready and unowned; cancel its own path |
| `Stop` | Stop manual control, Home, any planner or an active ToolSwap operation |

Unicast is a transport choice, not a two-node topology restriction. Every authorized PB addresses the Arm PB directly. One helper can serve multiple arms if it keeps separate sessions keyed by destination EntityId and arm name; each Arm PB independently lists that helper. No ToolSwap relay or central star is required. Multiple helpers can offload planning, geometry or monitoring into their own instruction budgets. Native joint state and the numerical solver still belong to the Arm PB; this release does not split a solver across PBs.

Service messages use the existing `AutoArm/4` tag and `[Link]` Version 4 envelope with the additional discriminator `Service=1`. The specialized ToolSwap protocol is unchanged. The Arm PB routes by authorized sender EntityId before parsing, with the same packet-size and per-callback instruction limits. A service peer cannot send ToolSwap BUILD, equipment descriptors, attachment transitions or RESUME.

### Service client contract

1. Send `HELLO` with `Service=1`, the target's `Arm`, a positive sender `Epoch` persisted/incremented on restart, increasing `Sequence`, and `Remote=0`. Accept replies only from that configured Arm PB with matching tag/version/arm/service and monotonically increasing reply epoch/sequence. `STATE` returns its `Epoch`, expected client `Remote`, current `Generation`, and your `Ack`.
2. Subsequent requests use that arm epoch in `Remote`, the returned revision in `Generation`, and the latest accepted arm reply Sequence in `Seen`. `STATUS` and `PING` may read/refresh state across revisions. `HELLO` also refreshes state; a new sender epoch cancels only that sender's active path. Responses are per-peer, not shared sequence counters. PATH requires acknowledgement of an arm STATE. An active owner must keep receiving/echoing newer replies as well as sending heartbeats: 0.5 seconds without either direction stops motion.
3. A `Plan` peer sends `PATH` with `Waypoints` (the same 1–32 pose rows), optional `Anchor`, and optional `Moves`, `Turns`, `Lines`. Omitted speed lists inherit HeadSpeed/HeadTurnSpeed; list entries of zero also inherit. Tolerances are 0.005 m and 0.2 degrees. All rows and policies validate before any motion changes. Invalid or unauthorized requests return `Error` and do not interrupt another controller.
4. A successful path returns the new `Generation`, `Owner` EntityId, and its `PathId` (your request Sequence). Retain the new revision and poll `PING` or `STATUS` at least every 0.1 seconds during motion; the 0.5-second watchdog cancels/zeros only when a planner owns motion. Polls report `State=Moving`, `Completed`, and `Count`. Final measured completion reports `State=Complete`, releases ownership and resumes ON hold. Do not treat receipt of a path as completion.
5. `STOP` is permitted for a `Stop` peer or the current path owner. It requires a current authenticated epoch and fresh sequence, but may use an older motion revision so a safety stop cannot lose a race with a newly accepted path. It never grants restart authority. Motion-changing PATH requests require the current revision; Stop, pilot input, reload, owner restart, lost communication or native faults revoke it. To move again, explicitly run `On` on the arm, refresh the session, and send a new request.

Every service `STATE` contains `Ack`, `Role`, `Owner`, `Ready`, `Enabled`, `Busy`, `State`, `Error`, `PathId`, `Completed`, `Count`, `DefaultMove` and `DefaultTurn`. When ready, `PoseP`, `PoseF`, `PoseU` are the authoritative world-space focus frame. `Busy` includes ToolSwap, Home and local/planner paths. ToolSwap retains its specialized exclusive topology lease; planners cannot take over a swap or another active path. An idle observer's failed reply does not stop a healthy arm.

For example, after a HELLO response supplies the epochs/revision, a planner's payload can contain:

```ini
[Link]
Version=4
Service=1
Arm=Arm 1
Epoch=7
Remote=12
Generation=19
Sequence=24
Seen=8
Operation=PATH
Anchor=0
Waypoints=0 0 10 0 0 -1 0 1 0 | 0 0 11 0 0 -1 0 1 0
Moves=0|0.05
Turns=0|2
Lines=false|true
```

The numeric epochs/revision/sequence and waypoints above are illustrative: use live session values and reachable targets. Send with `IGC.SendUnicastMessage(armPB.EntityId, "AutoArm/4", ini.ToString())`. A worker can compute its own path in another PB and submit it through this contract; an observer can consume pose/state without participating in motion ownership.

## Further modules

A collision service is not included. The Stop role provides an emergency-stop integration point, and planners can supply paths calculated elsewhere. Continuous collision nudging before solver/output commit needs an additional protocol and control boundary; this release does not implement it. Camera/voxel sensing belongs in a separate service. Merely broadcasting a path or Stop from an unregistered PB does not grant control.
