$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$priorCliHome = $env:DOTNET_CLI_HOME
Push-Location $workspace
try {
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet'
    & dotnet run --project tools/ScriptPack -- pack-pb AutoArm_Bench.txt AutoArm_Bench_Compact.txt
    if ($LASTEXITCODE -ne 0) { throw 'Bench packing failed.' }
    & dotnet run --project tools/PBCompileChecks -- --full AutoArm_Bench.txt AutoArm_Bench_Compact.txt
    if ($LASTEXITCODE -ne 0) { throw 'Combined Bench rewriting failed.' }
}
finally { Pop-Location; $env:DOTNET_CLI_HOME = $priorCliHome }
