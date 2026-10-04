$ErrorActionPreference = 'Stop'
try {
    $request = [Console]::In.ReadToEnd() | ConvertFrom-Json
    foreach ($name in @('System.Collections.Immutable', 'System.Reflection.Metadata', 'Microsoft.CodeAnalysis', 'Microsoft.CodeAnalysis.CSharp')) {
        [Reflection.Assembly]::LoadFrom((Join-Path $request.GameBin ($name + '.dll'))) | Out-Null
    }
    $options = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::new([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp6)
    $header = 'using System;using System.Collections.Generic;using System.Linq;using System.Text;using Sandbox.ModAPI.Ingame;using Sandbox.ModAPI.Interfaces;using SpaceEngineers.Game.ModAPI.Ingame;using VRage.Game;using VRage.Game.ModAPI.Ingame;using VRage.Game.ModAPI.Ingame.Utilities;using VRageMath;public class Program:MyGridProgram{'
    function Syntax-Errors([string]$source, [string]$label) {
        $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($header + $source + '}', $options, $label, [Text.Encoding]::UTF8, [Threading.CancellationToken]::None)
        @($tree.GetDiagnostics() | Where-Object { $_.Severity.ToString() -eq 'Error' })
    }
    foreach ($code in @(0x0560, 0x0588)) {
        $errors = @(Syntax-Errors ('int ' + [char]$code + ';') 'nonportable identifier control')
        if (-not ($errors | Where-Object { $_.Id -eq 'CS1056' })) { throw 'Native Framework Unicode regression no longer reproduces; revisit identifier policy.' }
    }
    foreach ($path in $request.Paths) {
        $source = [IO.File]::ReadAllText($path, [Text.UTF8Encoding]::new($false, $true))
        $errors = @(Syntax-Errors $source $path)
        if ($errors.Count) {
            $text = $header + $source + '}'
            $characters = @($errors | Where-Object { $_.Id -eq 'CS1056' } | ForEach-Object {
                'U+{0:X4}' -f [int]$text[$_.Location.SourceSpan.Start]
            } | Sort-Object -Unique)
            if ($characters.Count) { [Console]::Error.WriteLine('Rejected character code points: ' + ($characters -join ', ')) }
            $errors | Select-Object -First 15 | ForEach-Object { [Console]::Error.WriteLine($_.ToString()) }
            exit 1
        }
        [Console]::WriteLine('Native .NET Framework + installed C#6 parser: PASS (' + [IO.Path]::GetFileName($path) + ')')
    }
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
