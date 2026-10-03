# PB-safe syntax compression

The generated scripts receive a syntax/semantic pass after identifier, repetition and namespace compression. Readable control logic and configuration names are unchanged.

| Suggestion | Applied policy |
| --- | --- |
| Remove `global::` from aliases | Rebind each shortened alias to its original type/assembly. Keep the prefix when shortening would capture a different valid symbol. Keep qualified references to the outer Program. The prefix has eight characters: 25 removals save 200. |
| Expression bodies | Existing C#6 return methods/getters stay in ordinary packing. Changing a one-statement void method from `{Call();}` to `=>Call();` saves zero characters. Expression-bodied `get`/`set` accessors require C#7 and are rejected by the PB's C#6 parser. Block-preserving packing expands declaration arrows so the additional normalized-text reparse also succeeds. |
| Redundant braces | Flatten declaration-free standalone blocks; unwrap a single expression/return/throw/break/continue only where C# accepts an embedded statement. Preserve scopes with variables, labels or goto, and compound if bodies that could capture an else. |
| Redundant parentheses | Remove whole-condition, double and selected atomic-expression parentheses. Preserve precedence-sensitive casts and arithmetic groupings. Executable IL must remain identical. |
| `readonly` | Remove only private instance reference-field modifiers. Preserve readonly structs/value fields because removing their defensive copies can change mutations. Preserve static readonly fields. |
| `sealed` | Retain it: it can assist devirtualization, and its character cost is small. |
| Helper-class `public` | A generated namespace helper may be internal, with C# accessibility and executable IL checked. The actual public Program entry point stays public. |
| Equal constants | Consolidate private string constants into an accessible ancestor constant when the bound type/value and output cost agree. Guard nameof and keep all configuration/protocol values. |

Measured ordinary outputs before/after this pass:

| Script | Before | After | Saved |
| --- | ---: | ---: | ---: |
| Arm | 96,425 | 95,836 | 589 |
| ToolSwap | 57,839 | 57,432 | 407 |
| Collision | 26,573 | 26,519 | 54 |
| Bench | 23,565 | 23,536 | 29 |
| Prototype Arm | 95,570 | 94,974 | 596 |
| Prototype Planner | 40,082 | 39,980 | 102 |

Counts are UTF-16 code units, matching the PB limit. Arm now has 4,164 characters free. These are compression changes; motion gains, ownership, pairing and configuration formats remain unchanged.

Validation includes semantic alias shadowing, scope/declaration guards, dangling-else behavior, readonly defensive-copy preservation, equal constants/nameof, and original/changed executable IL for structural edits. The readable and actual compressed behavior suite passes 206,872 assertions.
The regenerated prototype also passes its readable/compact detour checks and both surveyed-arm round trips under live Collision.

`PBCompileChecks --combined` validates the installed type-safety and resource visitors and emits their rewritten tree, matching the compiler pipeline. `--full` retains the additional normalized-text reparse check; the installed visitor can leave an invalid serialized semicolon on expression-bodied declarations even though direct tree emission succeeds. Strict block-only outputs continue to pass that extra check. Neither mode bypasses memory-safety rewriting or aliases protected collection types.
