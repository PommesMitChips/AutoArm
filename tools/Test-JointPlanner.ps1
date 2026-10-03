param([switch]$Build,[switch]$Profile)
$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
$previousCli=$env:DOTNET_CLI_HOME
Push-Location $workspace
try {
    $env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.dotnet'
    if($Build){& (Join-Path $PSScriptRoot 'Build.ps1') -Track experimental -Collection JointPlanner}
    function Check-Run([string[]]$arguments){
        & dotnet run --project tools/ArmTests -- @arguments
        if($LASTEXITCODE -ne 0){throw "Joint planning simulation failed: $arguments"}
    }
    Check-Run @('experimental/AutoArm_JointPlanner_Prototype_Source.txt','--joint-planner','experimental/AutoArm_Planner_Prototype_Source.txt')
    Check-Run @('experimental/AutoArm_JointPlanner_Prototype_Compact.txt','--joint-planner','experimental/AutoArm_Planner_Prototype_Compact.txt')
    Check-Run @('experimental/AutoArm_JointPlanner_Prototype_Source.txt','--joint-planner-survey','tools/ArmTests/fixtures/planner-survey.txt','experimental/AutoArm_Planner_Prototype_Source.txt')
    if($Profile){Check-Run @('experimental/AutoArm_JointPlanner_Prototype_Source.txt','--joint-planner-profile','experimental/AutoArm_Planner_Prototype_Source.txt')}
}
finally {Pop-Location;$env:DOTNET_CLI_HOME=$previousCli}
