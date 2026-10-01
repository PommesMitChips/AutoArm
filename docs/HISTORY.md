# Versions

The working tree contains the current script, source fragments, examples and development tools. Previous releases are retained in Git commits and annotated tags, rather than parallel release folders.

| Tag | Preserved change | Compact characters |
| --- | --- | ---: |
| `v2.0` | Automatic topology discovery, signed parallel groups, task-space control and additive pose correction | 45,140 |
| `v2.1-preview` | Manual-priority correction, bounded target lead and adversarial comparison with the supplied HeadOnly script; earlier branding | 47,363 |
| `v2.1` | AutoArm naming and documented compatibility; includes the preserved review | 47,349 |
| `v2.2` | Zero-time command handling, topology snapshots every 30 active passes, cached status formatting and passive pivot comparison | 56,732 |
| `v2.3` | Readable configuration and retained load compensation for gravity hold | 66,629 |
| `v2.4` | HEAD/HRZ/VRT frames, active cockpit selection, optional mouse input and named toolbar actions | 69,408 |
| `v2.5` | Tool swaps, facing-merge region discovery, strict Format 3, support/attachment interlocks and startup/Home regression fixes | 99,883 |
| `v2.5.1` | PB-safe hidden-top occupancy, mounted coupling learning, persistent identity/pose checks and tighter compaction | 99,772 |
| `v2.6` | Named tool-side bases without per-tool teaching, shared pose commands, Unicode compaction and strengthened support/ownership guards | 99,238 |
| `v2.7` | Rotor-only tool couplers, fresh headless pickup from declared arm-tip pose, shared control logic and inferred declarations | 99,846 |
| `v2.8` | Automatic arm-end/rotor-part discovery, bare rotor focus, no arm Mount/offset/Instructions, bounded scans and field packing | 99,505 |
| `v2.8.1` | One-shot On setup and automatic manual-control resume after tool mounting | 99,777 |
| `v3.0` | Separate arm/ToolSwap PBs, acknowledged ordered paths, movement leases and restart/timeout fences | Arm 78,266; ToolSwap 48,330 |
| `v3.1` | Recorded parked orientation, inherited travel speeds, linear insertion/withdrawal and expected-topology continuation | Arm 79,911; ToolSwap 51,414 |
| `v3.2` | Additional direct PB peers, one-pass active joint validation and stale tool-weight pruning | Arm 84,975; ToolSwap 51,414 |
| `v4.0` | Multiple arm instances, inherited layout templates, scoped commands/sessions and marker-stored automatic tool setup | Arm 99,319; ToolSwap 68,170 |
| `v4.0.1` | Docking progress stability, configurable damping, generated settings, remote configuration reload and startup input recovery | Arm 98,238; ToolSwap 64,193 |
| `v4.0.2` | Known manual tool detachment returns to bare-arm control, with failed-swap cancellation and explicit-stop inhibition | Arm 99,119; ToolSwap 64,369 |
| `v4.0.3` | Pose-only Arm descriptor removes unassigned live field, unused branches and endpoint inventory enumeration | Arm 98,886; ToolSwap 64,369 |

Read a previous script without changing your working tree:

```powershell
git show v2.4:AutoArm_Compact.txt
git show v2.4:AutoArm_Source.txt
```

Or check out a complete historical snapshot:

```powershell
git switch --detach v2.4
```

Run `git switch -` to return to your previous branch. Tags retain their original notes and configuration examples as well as the top-level source and compact script. Historical folder names exist only in those snapshots. Earlier versions preserve combined source; the full maintainable fragments and current toolchain were introduced with v2.5.

Historical notes describe configuration and behavior at their own version. The current arm PB requires [global] Format 7 and the ToolSwap PB requires [global] Format 2. Re-enter previous preferences in the current examples after deleting older Custom Data; there is no migration.

The supplied `reference/MiningArm_v2_0_HeadOnly.txt` remains a behavior-comparison fixture required by the tests; it is not a parallel AutoArm release.
