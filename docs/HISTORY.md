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

Historical notes describe the configuration and behavior at their own version. In particular, their migration advice does not apply to v2.5: nonblank Custom Data must use Format 3. Re-enter previous preferences after deleting older Custom Data.

The supplied `reference/MiningArm_v2_0_HeadOnly.txt` remains a behavior-comparison fixture required by the tests; it is not a parallel AutoArm release.
