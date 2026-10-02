param([switch]$SourceOnly)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $workspace 'AutoArm_Source.txt'))
function Replace-One([string]$old, [string]$new) {
    if (($script:source.Split(@($old), [StringSplitOptions]::None)).Count -ne 2) { throw "Arm prototype hook changed: $old" }
    $script:source = $script:source.Replace($old, $new)
}
$source = $source.Replace('"5.0.4"', '"5.0.4-reallocation"')
Replace-One 'if (protection != null) protection.Limit(LastQdot);' 'if (protection != null) protection.Constrain(LastQdot);'
Replace-One 'public void Limit(double[] rates)' ([IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.ConstraintRepair.cs.txt')) + "`npublic void Limit(double[] rates)")
Replace-One 'ini.Set(LinkSection, "SafetyConstraints", Rows.Count);' 'ini.Set(LinkSection, "SafetyReallocated", Reallocated); ini.Set(LinkSection, "SafetyRepairPasses", RepairPasses); ini.Set(LinkSection, "SafetyRepairBudget", RepairBudget); ini.Set(LinkSection, "SafetyConstraints", Rows.Count);'
[IO.File]::WriteAllText((Join-Path $workspace 'AutoArm_Reallocation_Prototype_Source.txt'), $source, [Text.UTF8Encoding]::new($false))
if (-not $SourceOnly) { & (Join-Path $PSScriptRoot 'Build.ps1') -Source AutoArm_Reallocation_Prototype_Source.txt -Output AutoArm_Reallocation_Prototype_Compact.txt }
