param(
    [string]$GameBin = 'C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64',
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$parts = @('AutoArm.Prefix.cs.txt', 'AutoArm.Topology.cs.txt', 'AutoArm.Config.cs.txt', 'AutoArm.Control.cs.txt', 'AutoArm.Math.cs.txt', 'AutoArm.Pivot.cs.txt', 'AutoArm.TopInfo.cs.txt', 'AutoArm.ToolControl.cs.txt', 'AutoArm.Link.cs.txt', 'AutoArm.Path.cs.txt', 'AutoArm.ToolClient.cs.txt')
$sourcePath = Join-Path $workspace 'AutoArm_Source.txt'
$outputPath = Join-Path $workspace 'AutoArm_Compact.txt'
$source = ($parts | ForEach-Object { [IO.File]::ReadAllText((Join-Path $workspace ('src\' + $_))) }) -join "`n`n"
[IO.File]::WriteAllText($sourcePath, $source, [Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Build.ps1') -Source $sourcePath -Output $outputPath -GameBin $GameBin
$toolParts = @('AutoArm.ToolPrefix.cs.txt', 'AutoArm.ToolHost.cs.txt', 'AutoArm.TopInfo.cs.txt', 'AutoArm.ToolGeometry.cs.txt', 'AutoArm.Tools.cs.txt', 'AutoArm.Math.cs.txt', 'AutoArm.Link.cs.txt')
$toolSourcePath = Join-Path $workspace 'AutoArm_ToolSwap_Source.txt'
$toolOutputPath = Join-Path $workspace 'AutoArm_ToolSwap_Compact.txt'
$toolSource = ($toolParts | ForEach-Object { [IO.File]::ReadAllText((Join-Path $workspace ('src\' + $_))) }) -join "`n`n"
[IO.File]::WriteAllText($toolSourcePath, $toolSource, [Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Build.ps1') -Source $toolSourcePath -Output $toolOutputPath -GameBin $GameBin
if ($Test) {
    $oldCliHome = $env:DOTNET_CLI_HOME
    $oldGameBin = $env:SE_BIN
    $oldTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
    try {
        $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet'
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        $env:SE_BIN = $GameBin
        & dotnet run --project (Join-Path $PSScriptRoot 'ArmTests') "-p:GameBin=$GameBin" -- $sourcePath
        if ($LASTEXITCODE -ne 0) { throw 'Automatic-arm regression checks failed.' }
    }
    finally {
        $env:DOTNET_CLI_HOME = $oldCliHome
        $env:SE_BIN = $oldGameBin
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = $oldTelemetry
    }
}
