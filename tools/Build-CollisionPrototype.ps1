param([switch]$SourceOnly)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.Collision.cs.txt'))
function Replace-One([string]$old, [string]$new) {
    if (($script:source.Split(@($old), [StringSplitOptions]::None)).Count -ne 2) { throw "Prototype hook changed: $old" }
    $script:source = $script:source.Replace($old, $new)
}
Replace-One '"5.0.5"' '"5.0.5-self-pairs"'
Replace-One 'Ready = true; Arms.Clear(); RefreshScene(true);' 'ConfigureSelfPairs(ini); Ready = true; Arms.Clear(); RefreshScene(true);'
Replace-One 'shape.Cursor = shape.Min; shape.Shapes.Clear();' 'shape.Cursor = shape.Min; SelfGeometryRevision++; shape.Shapes.Clear();'
Replace-One 'TilesVisited = CandidatesSeen = NarrowChecks = 0;' 'PrepareSelfPairs(arm); TilesVisited = CandidatesSeen = NarrowChecks = 0;'
Replace-One 'double range = QueryRange(arm, pair.Value, body, obstacle);' "double range = QueryRange(arm, pair.Value, body, obstacle);`n                if (SkipSelfPair(arm, body, obstacle, range)) continue;"
Replace-One '+ (Detailed ? ContactAudit() : ""));' '+ (Detailed ? ContactAudit() : "") + SelfPairInfo());'
$source += "`n" + [IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.SelfPairs.cs.txt'))
[IO.File]::WriteAllText((Join-Path $workspace 'AutoArm_Collision_Prototype_Source.txt'), $source, [Text.UTF8Encoding]::new($false))
if (-not $SourceOnly) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -Source AutoArm_Collision_Prototype_Source.txt -Output AutoArm_Collision_Prototype_Compact.txt -Fast -PreserveBodies
}
