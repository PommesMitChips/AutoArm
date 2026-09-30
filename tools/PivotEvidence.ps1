param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers'
)

# Read-only evidence collection. Imports only model dummy tags using the installed
# game reader; does not launch the game, change assets, or modify a save.
$ErrorActionPreference = 'Stop'
$gameBin = Join-Path $GameRoot 'Bin64'
$contentRoot = Join-Path $GameRoot 'Content'
$definitionsPath = Join-Path $contentRoot 'Data\CubeBlocks\CubeBlocks_Mechanical.sbc'
foreach ($assemblyName in @('VRage.Math.dll', 'VRage.Library.dll', 'VRage.dll', 'VRage.Render.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $gameBin $assemblyName)) | Out-Null
}
[xml]$definitions = Get-Content -LiteralPath $definitionsPath
$subtypes = @(
    'LargeStator', 'SmallStator', 'LargeAdvancedStator',
    'SmallAdvancedStator', 'SmallAdvancedStatorSmall',
    'LargeHinge', 'MediumHinge', 'SmallHinge'
)
$rows = foreach ($definition in $definitions.Definitions.CubeBlocks.Definition) {
    $subtype = [string]$definition.Id.SubtypeId
    if ($subtypes -notcontains $subtype) { continue }
    $modelPath = Join-Path $contentRoot ([string]$definition.Model)
    $importer = New-Object VRageRender.Import.MyModelImporter
    $importer.ImportData($modelPath, [string[]]@('Dummies'))
    $dummy = $importer.GetTagData()['Dummies']['electric_motor']
    if ($null -eq $dummy) { throw "Missing electric_motor dummy: $subtype" }
    $matrix = $dummy.Matrix
    $x = [double]$matrix.M41
    $y = [double]$matrix.M42
    $z = [double]$matrix.M43
    [PSCustomObject]@{
        Subtype = $subtype
        ModelPath = $modelPath
        ModelSha256 = (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash
        Dummy = 'electric_motor'
        LocalTranslationMetres = @($x, $y, $z)
        PerpendicularDistanceToLocalUpMetres = [Math]::Sqrt($x * $x + $z * $z)
    }
}
if (@($rows).Count -ne $subtypes.Count) { throw 'One or more expected vanilla definitions were not found.' }
[PSCustomObject]@{
    Scope = 'Installed vanilla base models listed below; does not establish a claim about modded models or live constraint deformation.'
    Method = 'VRageRender.Import.MyModelImporter.ImportData(modelPath, new string[] { "Dummies" }); GetTagData()["Dummies"]["electric_motor"].Matrix.Translation'
    DefinitionPath = $definitionsPath
    DefinitionSha256 = (Get-FileHash -LiteralPath $definitionsPath -Algorithm SHA256).Hash
    ImporterAssemblyPath = Join-Path $gameBin 'VRage.Render.dll'
    ImporterAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path $gameBin 'VRage.Render.dll') -Algorithm SHA256).Hash
    Models = @($rows)
} | ConvertTo-Json -Depth 5
