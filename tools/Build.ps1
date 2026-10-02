param(
    [string]$Source = 'AutoArm_Source.txt',
    [string]$Output = 'AutoArm_Compact.txt',
    [string]$GameBin = 'C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64',
    [switch]$Test,
    [switch]$Fast,
    [switch]$PreserveBodies
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$oldCliHome = $env:DOTNET_CLI_HOME
$oldGameBin = $env:SE_BIN
$oldTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$oldFirstRun = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
$oldCertificates = $env:DOTNET_GENERATE_ASPNET_CERTIFICATE
Push-Location $workspace
try {
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:SE_BIN = $GameBin
    $packingMode = if ($PreserveBodies) { 'pack-pb' } elseif ($Fast) { 'pack-fast' } else { 'pack' }
    & dotnet run --project tools/ScriptPack -- $packingMode $Source $Output
    if ($LASTEXITCODE -ne 0) { throw 'Packing/validation failed.' }
    if ($Test) {
        & dotnet run --project tools/ArmTests "-p:GameBin=$GameBin" -- $Source
        if ($LASTEXITCODE -ne 0) { throw 'Behavior regression check failed.' }
    }
}
finally {
    Pop-Location
    $env:DOTNET_CLI_HOME = $oldCliHome
    $env:SE_BIN = $oldGameBin
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $oldTelemetry
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $oldFirstRun
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = $oldCertificates
}
