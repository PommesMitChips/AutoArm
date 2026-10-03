# Reallocation

Experimental arm constrained-rate repair variant. Its editable repair algorithm is
`../shared/AutoArm.ConstraintRepair.cs.txt`; the guarded insertion recipe is
`../../../tools/build/Recipes.psm1` (`Invoke-ReallocationRecipe`). There is no
duplicated algorithm in this folder. Build with `./tools/Build.ps1 -Track experimental
-Scripts Reallocation` from the repository root. Do not edit generated outputs.

## Existing WIP snapshot

`Reallocation.Program.cs.txt` is the complete editable WIP snapshot preserved from
the pre-cleanup working tree, with release labels normalized to 1.1. Normal
builds assemble/compact this file. The partial algorithm files are retained for
`-RefreshPrototypes`, which explicitly applies the guarded recipes to the current
core; that refresh can reveal unresolved size/compatibility limits.
