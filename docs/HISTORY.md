# Reconstructed version history

The original workspace was not a Git repository. Its saved releases were imported in development order, with one commit and annotated tag per snapshot. These are reconstructed snapshots, not recovered original commits. Git timestamps identify this import, not the time each feature was written. Historical filenames and release notes are preserved without edits.

| Tag | Preserved change | Compact characters |
| --- | --- | ---: |
| `v2.0` | Automatic topology discovery, signed parallel groups, task-space control and additive pose correction | 45,140 |
| `v2.1-preview` | Manual-priority correction, bounded target lead and adversarial comparison with the supplied HeadOnly script; earlier branding | 47,363 |
| `v2.1` | AutoArm naming and documented compatibility; includes the preserved review | 47,349 |
| `v2.2` | Zero-time command handling, topology snapshots every 30 active passes, cached status formatting and passive pivot comparison | 56,732 |
| `v2.3` | Readable configuration and retained load compensation for gravity hold | 66,629 |
| `v2.4` | HEAD/HRZ/VRT frames, active cockpit selection, optional mouse input and named toolbar actions | 69,408 |
| `v2.5` | Tool swaps, facing-merge region discovery, strict Format 3, support/attachment interlocks and startup/Home regression fixes | 99,883 |

`main` contains the imported releases through v2.4. The `release/v2.5-tool-head-swap` branch and its pull request contain v2.5, the current development fragments, build tools, tests and setup examples. The v2.5 tag identifies that branch's imported release snapshot; its presence does not imply that the PR was merged or that live game physics were validated.

Each commit updates the top-level `AutoArm_Source.txt` and `AutoArm_Compact.txt`. Exact delivered artifacts live under `releases/<tag>/`. Earlier maintainable source fragments were not preserved independently, so the historical tags supply the combined source rather than pretending the current fragments were used to build earlier versions. The complete current toolchain is introduced with v2.5.

Historical notes describe the configuration and behavior at their own version. In particular, their migration advice does not apply to v2.5: nonblank Custom Data must use Format 3. Re-enter previous preferences after deleting older Custom Data.

Pre-rewrite mining-arm adaptations and setup probes were retained in the local workspace archive, outside this AutoArm repository. The supplied `reference/MiningArm_v2_0_HeadOnly.txt` remains a behavior-comparison fixture used by the tests.
