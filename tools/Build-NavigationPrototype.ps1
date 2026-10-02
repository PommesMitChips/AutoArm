param([switch]$SourceOnly)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
function Edit-One([string]$text,[string]$old,[string]$new) {
    if (($text.Split(@($old),[StringSplitOptions]::None)).Count -ne 2) {throw "Navigation hook changed: $old"}
    return $text.Replace($old,$new)
}
$arm = [IO.File]::ReadAllText((Join-Path $workspace 'AutoArm_Source.txt')).Replace('"5.0.4"','"5.0.5-passing"')
$arm = Edit-One $arm 'public void Limit(double[] rates)' ([IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.NavigationArm.cs.txt'))+"`npublic void Limit(double[] rates)")
$arm = Edit-One $arm 'public void Limit(double[] rates)' ([IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.ConstraintRepair.cs.txt'))+"`npublic void Limit(double[] rates)")
$arm = Edit-One $arm 'if (protection != null) protection.Limit(LastQdot);' 'if (protection != null) protection.Constrain(LastQdot);'
$arm = Edit-One $arm 'ini.Set(LinkSection, "SafetyConstraints", Rows.Count);' 'ini.Set(LinkSection, "SafetyReallocated", Reallocated); ini.Set(LinkSection, "SafetyRepairPasses", RepairPasses); ini.Set(LinkSection, "SafetyRepairBudget", RepairBudget); ini.Set(LinkSection, "SafetyConstraints", Rows.Count);'
$arm = Edit-One $arm 'ini.Set(LinkSection, "Caps", caps.ToString());' 'WriteNavigation(ini); ini.Set(LinkSection, "Caps", caps.ToString());'
$arm = Edit-One $arm 'Sent.Remove(ack); Bias = bias;' 'ReadNavigation(ini); Sent.Remove(ack); Bias = bias;'
$arm = Edit-One $arm 'ini.Set(LinkSection, "SafetyConstraints", Rows.Count);' 'NavigationStatus(ini); ini.Set(LinkSection, "SafetyConstraints", Rows.Count);'
$arm = Edit-One $arm 'public bool Docking, Capture;' 'public bool Docking, Capture; public string NavBudgetKey = ""; public double NavBudgetDistance;'
$arm = Edit-One $arm 'MatrixD goal = path.Current, demand = goal;' "MatrixD goal = path.Current, demand = goal;`n    if (Services != null && Services.Safety != null) Services.Safety.Navigate(path, ref demand);"
[IO.File]::WriteAllText((Join-Path $workspace 'AutoArm_Passing_Prototype_Source.txt'),$arm,[Text.UTF8Encoding]::new($false))
$collision = [IO.File]::ReadAllText((Join-Path $workspace 'AutoArm_Collision_Source.txt')).Replace('"5.0.5"','"5.0.5-passing"')
$collision = Edit-One $collision 'public int AuditIndex = -1;' 'public bool NavAllowed; public string NavKey; public Vector3D NavHead, NavGoal; public double NavSpeed; public PassingLane Lane; public int AuditIndex = -1;'
$collision = Edit-One $collision 'ParseArm(frame, ini);' 'ParseArm(frame, ini); ReadNavigation(frame, ini, arm);'
$collision = Edit-One $collision 'scale = 0;' 'if (!arm.NavAllowed) scale = 0;'
$collision = Edit-One $collision 'Status = arm.Name + ": " + reason;' 'Status = arm.Name + ": " + reason; PassingGuidance(arm, response);'
$collision += "`n" + [IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.NavigationCollision.cs.txt'))
[IO.File]::WriteAllText((Join-Path $workspace 'AutoArm_Collision_Passing_Prototype_Source.txt'),$collision,[Text.UTF8Encoding]::new($false))
$priorCliHome=$env:DOTNET_CLI_HOME
try {
    $env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.dotnet'
    & dotnet run --project (Join-Path $PSScriptRoot 'ScriptPack') -- blocks (Join-Path $workspace 'AutoArm_Passing_Prototype_Source.txt') (Join-Path $workspace 'AutoArm_Passing_Prototype_Source.txt')
    if ($LASTEXITCODE -ne 0) {throw 'Navigation source block expansion failed.'}
} finally {$env:DOTNET_CLI_HOME=$priorCliHome}
if (-not $SourceOnly) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -Source AutoArm_Passing_Prototype_Source.txt -Output AutoArm_Passing_Prototype_Compact.txt -PreserveBodies
    & (Join-Path $PSScriptRoot 'Build.ps1') -Source AutoArm_Collision_Passing_Prototype_Source.txt -Output AutoArm_Collision_Passing_Prototype_Compact.txt -PreserveBodies
    $priorCliHome=$env:DOTNET_CLI_HOME
    try {
        $env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.dotnet'
        foreach($name in @('AutoArm_Passing_Prototype_Compact.txt','AutoArm_Collision_Passing_Prototype_Compact.txt')) {
            $path=Join-Path $workspace $name
            & dotnet run --project (Join-Path $PSScriptRoot 'ScriptPack') -- blocks-min $path $path
            if ($LASTEXITCODE -ne 0) {throw 'Generated forwarding body expansion failed.'}
            if ([IO.File]::ReadAllText($path).Length -gt 100000) {throw 'Prototype exceeds PB editor limit.'}
        }
    } finally {$env:DOTNET_CLI_HOME=$priorCliHome}
}
