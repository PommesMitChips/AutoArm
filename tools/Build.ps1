<#
.SYNOPSIS
Build selected Space Engineers scripts or the shared graphics/tool assets.
.EXAMPLE
./tools/Build.ps1 -Track experimental -Scripts AutoArm,ToolSwap
.EXAMPLE
./tools/Build.ps1 -Track stable -Collection Release -Test
.EXAMPLE
./tools/Build.ps1 -Collection Assets
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('stable','experimental')][string]$Track = 'experimental',
    [string[]]$Scripts = @('All'),
    [ValidateSet('Core','Diagnostics','Release','Prototypes','JointPlanner','Assets')][string]$Collection,
    [string]$GameBin = 'C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64',
    [string]$Python = 'python',
    [switch]$SourceOnly,
    [switch]$RefreshPrototypes,
    [switch]$Test,
    [switch]$List
)
$ErrorActionPreference = 'Stop'
if ($Test -and $SourceOnly) { throw '-Test requires packed scripts; omit -SourceOnly.' }
$RepositoryRoot = Split-Path -Parent $PSScriptRoot
$trackRoot = Join-Path $RepositoryRoot $Track
Import-Module (Join-Path $RepositoryRoot 'tools/build/Recipes.psm1') -Force -DisableNameChecking
Set-BuildContext -TrackRoot $trackRoot -RepoRoot $RepositoryRoot -GameBin $GameBin
$available = @('AutoArm','ToolSwap','Collision','Bench','Survey')
if ($Track -eq 'experimental') { $available += @('Reallocation','CollisionSelfPairs','PassingArm','PassingCollision','JointPlannerArm','Planner') }
if ($List) { $available; 'Assets: Blocks,Models,SVGPanels,PanelAssembler,WorkshopPanels,ToolSwapPanels,CommandPanels,WorkshopPack'; return }
$assetBuilders = @{
    Blocks=@('blocks/build.py'); Models=@('models/build.py')
    SVGPanels=@('workshop/svg-panels/build.py','--strip-separators')
    PanelAssembler=@('../panel-assembler/build.py')
    WorkshopPanels=@('workshop/build_panels.py'); ToolSwapPanels=@('workshop/tool-swap/build.py')
    CommandPanels=@('workshop/commands/build.py'); WorkshopPack=@('workshop/pack_all.py')
}
$assetSelection = if ($Collection -eq 'Assets') { @('SVGPanels','PanelAssembler') }
                  elseif (-not $Collection) { @($Scripts | ForEach-Object { $_ -split ',' } | Where-Object { $_ -in $assetBuilders.Keys }) }
                  else { @() }
foreach ($name in @($Scripts | ForEach-Object { $_ -split ',' })) {
    if (-not $Collection -and $name -ne 'All' -and $name -notin $available -and $name -notin $assetBuilders.Keys) { throw "Unknown/unavailable script '$name' for $Track. Use -List." }
}
if ($assetSelection.Count) {
    if ($PSCmdlet.ShouldProcess(($assetSelection -join ', '),'Build shared assets/tools')) {
        foreach ($asset in $assetSelection) {
            $task=$assetBuilders[$asset]
            $path = Join-Path $RepositoryRoot ('tools/asset-builders/' + $task[0])
            & $Python $path @($task | Select-Object -Skip 1)
            if ($LASTEXITCODE -ne 0) { throw "Asset build failed: $($task[0])" }
        }
    }
    if ($Collection -eq 'Assets') { return }
}
$requested = if ($Collection) {
    switch ($Collection) {
        Core { @('AutoArm','ToolSwap','Collision') }
        Diagnostics { @('Bench','Survey') }
        Release { @('AutoArm','ToolSwap','Collision','Bench','Survey') }
        Prototypes { @('Reallocation','CollisionSelfPairs','PassingArm','PassingCollision','JointPlannerArm','Planner') }
        JointPlanner { @('JointPlannerArm','Planner') }
    }
} else { @($Scripts | ForEach-Object { $_ -split ',' } | Where-Object { $_ -notin $assetBuilders.Keys }) }
if (-not $requested.Count) { return }
if ($requested -contains 'All') { $requested = $available }
foreach ($name in $requested) { if ($name -notin $available) { throw "Unknown/unavailable script '$name' for $Track. Use -List." } }
if ($Test) { $requested = @('AutoArm','ToolSwap','Collision','Bench','Survey') + $requested }
# Prototype recipes depend on the current assembled arm/collision source.
if (@($requested | Where-Object { $_ -notin @('AutoArm','ToolSwap','Collision','Bench','Survey') }).Count) {
    $requested = @('AutoArm','Collision') + $requested
}
$requested = @($requested | Select-Object -Unique)
if (-not $PSCmdlet.ShouldProcess("$Track : $($requested -join ', ')",'Build and validate')) { return }
$oldCli = $env:DOTNET_CLI_HOME; $oldBin = $env:SE_BIN; $oldTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
Push-Location $trackRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $RepositoryRoot 'tools/.dotnet'
    $env:SE_BIN = $GameBin; $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $done = @{}
    foreach ($name in $requested) {
        if (-not $RefreshPrototypes -and $name -in @('Reallocation','PassingArm','PassingCollision','JointPlannerArm','Planner')) {
            Build-PrototypeSnapshot $name -SourceOnly:$SourceOnly
            continue
        }
        switch ($name) {
            { $_ -in @('AutoArm','ToolSwap','Collision','Bench','Survey') } { Build-CoreScript $name -SourceOnly:$SourceOnly }
            Reallocation { Invoke-ReallocationRecipe $trackRoot $RepositoryRoot -SourceOnly:$SourceOnly }
            CollisionSelfPairs { Invoke-SelfPairsRecipe $trackRoot $RepositoryRoot -SourceOnly:$SourceOnly }
            { $_ -in @('PassingArm','PassingCollision') } { if (-not $done.Passing) { Invoke-PassingRecipe $trackRoot $RepositoryRoot -SourceOnly:$SourceOnly; $done.Passing=$true } }
            { $_ -in @('JointPlannerArm','Planner') } { if (-not $done.Planning) { Invoke-JointPlannerRecipe $trackRoot $RepositoryRoot -SourceOnly:$SourceOnly; $done.Planning=$true } }
        }
    }
    if ($Test) {
        Invoke-ManagedTool ArmTests @((Join-Path $trackRoot 'AutoArm_Source.txt'))
        Invoke-ManagedTool ArmTests @((Join-Path $trackRoot 'AutoArm_Source.txt'),'--bench-compact')
    }
}
finally { Pop-Location; $env:DOTNET_CLI_HOME=$oldCli; $env:SE_BIN=$oldBin; $env:DOTNET_CLI_TELEMETRY_OPTOUT=$oldTelemetry }
