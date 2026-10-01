param(
    [string]$GameBin = 'C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64',
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$parts = @('AutoArm.Prefix.cs.txt', 'AutoArm.Topology.cs.txt', 'AutoArm.Config.cs.txt', 'AutoArm.Control.cs.txt', 'AutoArm.Math.cs.txt', 'AutoArm.Pivot.cs.txt', 'AutoArm.TopInfo.cs.txt', 'AutoArm.ToolControl.cs.txt', 'AutoArm.Link.cs.txt', 'AutoArm.Path.cs.txt', 'AutoArm.ToolClient.cs.txt', 'AutoArm.Services.cs.txt')
$sourcePath = Join-Path $workspace 'AutoArm_Source.txt'
$outputPath = Join-Path $workspace 'AutoArm_Compact.txt'
function Specialize-Source([string]$path) {
    $priorCliHome = $env:DOTNET_CLI_HOME
    $priorGameBin = $env:SE_BIN
    try {
        $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet'
        $env:SE_BIN = $GameBin
        & dotnet run --project (Join-Path $PSScriptRoot 'ScriptPack') -- specialize $path $path
        if ($LASTEXITCODE -ne 0) { throw 'PB source specialization failed.' }
    }
    finally { $env:DOTNET_CLI_HOME = $priorCliHome; $env:SE_BIN = $priorGameBin }
}
$source = ($parts | ForEach-Object { [IO.File]::ReadAllText((Join-Path $workspace ('src\' + $_))) }) -join "`n`n"
function Build-HostScript([string]$engine, [bool]$tool) {
    $hostText = [IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.Host.cs.txt')).Replace('@@TOOL@@',$(if ($tool) {'true'} else {'false'})).Replace('@@FORMAT@@',$(if ($tool) {'2'} else {'7'}))
    $coreText = [regex]::Replace($engine,'\bProgram\b','ArmCore')
    $coreText = $coreText.Replace('readonly string ArmName = "Arm 1";','readonly string ArmName;')
    $coreText = $coreText.Replace('Me.CustomData','CustomData').Replace('Runtime.UpdateFrequency','Frequency').Replace('Runtime.TimeSinceLastRun.TotalSeconds','Elapsed')
    $coreText = $coreText.Replace('P.IGC.UnicastListener.HasPendingMessage','P.Inbox.Count > 0').Replace('P.IGC.UnicastListener.AcceptMessage()','P.Inbox.Dequeue()')
    $coreText = $coreText.Replace('IGC.UnicastListener.HasPendingMessage','Inbox.Count > 0').Replace('IGC.UnicastListener.AcceptMessage()','Inbox.Dequeue()')
    $coreText = $coreText.Replace('"AutoArm/4"','"AutoArm/5"').Replace('"Version", 4','"Version", 5').Replace('"Version").ToInt32() == 4','"Version").ToInt32() == 5')
    $coreText = [regex]::Replace($coreText,'public ArmCore\(\)\s*\{','public ArmCore(Program host, string name, string customData, string storage) { Host = host; ArmName = name; CustomData = customData; Storage = storage;')
    if (-not $tool) { $coreText = $coreText.Replace('bool CheckArmLayout(out string why) { why = ""; return true; }','bool CheckArmLayout(out string why) { return Host.Layout(this, out why); }') }
    else { $coreText = $coreText.Replace('if (!Tools.Load(out why)) throw new Exception(why);','if (!Tools.Load(out why) || !Tools.NamesForArm(out why)) throw new Exception(why);') }
    $facadeText = [IO.File]::ReadAllText((Join-Path $workspace 'src/AutoArm.CoreFacade.cs.txt'))
    $roleText = [IO.File]::ReadAllText((Join-Path $workspace $(if ($tool) {'src/AutoArm.ToolFacade.cs.txt'} else {'src/AutoArm.ArmFacade.cs.txt'})))
    return $hostText + "`nsealed class ArmCore {`n" + $facadeText + "`n" + $coreText + "`n" + $roleText + "`n}`n"
}
$engineDir = Join-Path $PSScriptRoot 'ScriptPack/obj'
[IO.Directory]::CreateDirectory($engineDir) | Out-Null
[IO.File]::WriteAllText((Join-Path $engineDir 'AutoArm.Engine.txt'),$source,[Text.UTF8Encoding]::new($false))
$source = Build-HostScript $source $false
[IO.File]::WriteAllText($sourcePath, $source, [Text.UTF8Encoding]::new($false))
Specialize-Source $sourcePath
& (Join-Path $PSScriptRoot 'Build.ps1') -Source $sourcePath -Output $outputPath -GameBin $GameBin
$toolParts = @('AutoArm.ToolPrefix.cs.txt', 'AutoArm.ToolHost.cs.txt', 'AutoArm.TopInfo.cs.txt', 'AutoArm.ToolGeometry.cs.txt', 'AutoArm.Tools.cs.txt', 'AutoArm.ToolDiscovery.cs.txt', 'AutoArm.ParkPoses.cs.txt', 'AutoArm.Math.cs.txt', 'AutoArm.Link.cs.txt')
$toolSourcePath = Join-Path $workspace 'AutoArm_ToolSwap_Source.txt'
$toolOutputPath = Join-Path $workspace 'AutoArm_ToolSwap_Compact.txt'
$toolSource = ($toolParts | ForEach-Object { [IO.File]::ReadAllText((Join-Path $workspace ('src\' + $_))) }) -join "`n`n"
[IO.File]::WriteAllText((Join-Path $engineDir 'AutoArm.ToolEngine.txt'),$toolSource,[Text.UTF8Encoding]::new($false))
$toolSource = Build-HostScript $toolSource $true
[IO.File]::WriteAllText($toolSourcePath, $toolSource, [Text.UTF8Encoding]::new($false))
Specialize-Source $toolSourcePath
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
