using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class ScriptPack
{
    internal const string Header = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text;\nusing Sandbox.ModAPI.Ingame;\nusing Sandbox.ModAPI.Interfaces;\nusing SpaceEngineers.Game.ModAPI.Ingame;\nusing VRage.Game;\nusing VRage.Game.ModAPI.Ingame;\nusing VRage.Game.ModAPI.Ingame.Utilities;\nusing VRageMath;\npublic class Program : MyGridProgram {\n";
    internal static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp6);
    internal static string GameBin = Environment.GetEnvironmentVariable("SE_BIN") ?? @"C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64";
    static readonly List<MetadataReference> References = LoadReferences();

    static List<MetadataReference> LoadReferences()
    {
        var framework = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\Framework64\v4.0.30319");
        var paths = new[] { "mscorlib.dll", "System.dll", "System.Core.dll", "System.Xml.dll" }.Select(p => Path.Combine(framework, p))
            .Concat(new[] { "netstandard.dll", "Sandbox.Common.dll", "Sandbox.Game.dll", "SpaceEngineers.Game.dll", "VRage.dll", "VRage.Game.dll", "VRage.Library.dll", "VRage.Math.dll" }.Select(p => Path.Combine(GameBin, p)));
        return paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }

    internal static CSharpCompilation Compile(string source, string label)
    {
        var tree = CSharpSyntaxTree.ParseText(Header + source + "\n}", ParseOptions, label);
        return CSharpCompilation.Create("Arm_" + Guid.NewGuid().ToString("N"), new[] { tree }, References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
    }

    internal static void Check(CSharpCompilation compilation)
    {
        using var dll = new MemoryStream();
        var result = compilation.Emit(dll);
        foreach (var d in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(20)) Console.Error.WriteLine(d);
        if (!result.Success) throw new Exception("C# 6 compilation failed.");
    }

    static void Main(string[] args)
    {
        try
        {
            if (args.Length < 2) throw new Exception("Usage: ScriptPack check|inspect|pack <source.txt> [output.txt]");
            var source = File.ReadAllText(args[1]);
            var compilation = Compile(source, args[1]);
            Check(compilation);
            Console.WriteLine($"C# 6 / installed SE API: PASS ({source.Length:N0} UTF-16 characters)");
            if (args[0] == "check") return;
            if (args[0] == "inspect") { Inspect(compilation); return; }
            if (args[0] == "test") { Regression.Run(args[1], args[2]); return; }
            if (args[0] != "pack" || args.Length != 3) throw new Exception("Unknown command or missing output path.");
            Pack(source, compilation, args[2]);
        }
        catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
    }

    static void Inspect(CSharpCompilation compilation)
    {
        var tree = compilation.SyntaxTrees.Single();
        var root = tree.GetRoot();
        var model = compilation.GetSemanticModel(tree);
        var counts = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);
        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(name).Symbol;
            if (symbol != null) counts[symbol] = counts.GetValueOrDefault(symbol) + 1;
        }
        foreach (var node in root.DescendantNodes().Where(n => n is MethodDeclarationSyntax or PropertyDeclarationSyntax or VariableDeclaratorSyntax))
        {
            var symbol = model.GetDeclaredSymbol(node);
            if (symbol is ILocalSymbol || symbol == null) continue;
            if (counts.GetValueOrDefault(symbol) == 0) Console.WriteLine($"Unreferenced: {symbol.ToDisplayString()} (line {node.GetLocation().GetLineSpan().StartLinePosition.Line - 10})");
        }
        Console.WriteLine("Largest methods:");
        foreach (var m in root.DescendantNodes().OfType<MethodDeclarationSyntax>().OrderByDescending(m => m.Span.Length).Take(16))
            Console.WriteLine($"{m.Span.Length,6} {m.Identifier.Text}");
    }

    static void Pack(string source, CSharpCompilation compilation, string output)
    {
        var tree = compilation.SyntaxTrees.Single();
        var root = tree.GetRoot();
        var model = compilation.GetSemanticModel(tree);
        var program = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        int settingsEnd = source.IndexOf("static string MiningTuningError", StringComparison.Ordinal);
        if (settingsEnd < 0) settingsEnd = source.IndexOf("// ===== IMPLEMENTATION =====", StringComparison.Ordinal);
        if (settingsEnd < 0) throw new Exception("Missing settings boundary.");
        int boundary = Header.Length + settingsEnd;
        // Keep settings, action enums and toolbar data readable and directly editable.
        var protectedNames = root.DescendantTokens().Where(t => t.SpanStart < boundary && t.IsKind(SyntaxKind.IdentifierToken))
            .Select(t => t.ValueText).ToHashSet(StringComparer.Ordinal);
        protectedNames.UnionWith(new[] { "Main", "Save", "Program" });
        // Rename only script-owned, semantically bound symbols. API members and strings are never rewritten.
        // Keep enum names because their ToString() output is user-visible; keep override families together.
        var symbols = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        var byToken = new Dictionary<int, ISymbol>();
        ISymbol? SymbolFor(SyntaxToken t)
        {
            var p = t.Parent;
            ISymbol? s = p switch
            {
                SimpleNameSyntax n => model.GetSymbolInfo(n).Symbol,
                VariableDeclaratorSyntax n => model.GetDeclaredSymbol(n),
                ParameterSyntax n => model.GetDeclaredSymbol(n),
                MethodDeclarationSyntax n => model.GetDeclaredSymbol(n),
                PropertyDeclarationSyntax n => model.GetDeclaredSymbol(n),
                ClassDeclarationSyntax n => model.GetDeclaredSymbol(n),
                ConstructorDeclarationSyntax n => model.GetDeclaredSymbol(n)?.ContainingType,
                ForEachStatementSyntax n when n.Identifier == t => model.GetDeclaredSymbol(n),
                CatchDeclarationSyntax n when n.Identifier == t => model.GetDeclaredSymbol(n),
                _ => null
            };
            if (s is IMethodSymbol { MethodKind: MethodKind.Constructor } ctor) s = ctor.ContainingType;
            if (s is IMethodSymbol method)
            {
                while (method.OverriddenMethod != null) method = method.OverriddenMethod;
                s = method.OriginalDefinition;
            }
            if (s is IPropertySymbol property)
            {
                while (property.OverriddenProperty != null) property = property.OverriddenProperty;
                s = property.OriginalDefinition;
            }
            return s;
        }
        foreach (var token in program.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)))
        {
            var symbol = SymbolFor(token);
            if (symbol == null || symbol.IsImplicitlyDeclared || protectedNames.Contains(symbol.Name) || symbol.Name.Length <= 2 ||
                !symbol.Locations.Any(l => l.IsInSource) || symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum } ||
                symbol is IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum }) continue;
            if (symbol is not (IFieldSymbol or IMethodSymbol or IPropertySymbol or INamedTypeSymbol or ILocalSymbol or IParameterSymbol)) continue;
            byToken[token.SpanStart] = symbol;
        }
        var taken = root.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Select(t => t.ValueText).ToHashSet(StringComparer.Ordinal);
        int next = 0;
        foreach (var group in byToken.Values.GroupBy(s => s, SymbolEqualityComparer.Default).OrderByDescending(g => g.Count() * (g.Key!.Name.Length - 2)))
        {
            string name;
            do { name = ShortName(next++); } while (taken.Contains(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None);
            if (name.Length >= group.Key!.Name.Length) continue;
            taken.Add(name);
            symbols[group.Key] = name;
        }
        var tokens = program.DescendantTokens().Where(t => t.SpanStart >= boundary && t != program.CloseBraceToken && t.Span.Length > 0).ToArray();
        var renamed = tokens.Select(t => byToken.TryGetValue(t.SpanStart, out var s) && symbols.TryGetValue(s, out var n) ? SyntaxFactory.Identifier(n) : t.WithoutTrivia()).ToArray();
        var compact = Compact(renamed);
        // Full token round-trip, not a delimiter-count approximation.
        var reparsed = SyntaxFactory.ParseTokens(compact, options: ParseOptions).Where(t => !t.IsKind(SyntaxKind.EndOfFileToken)).ToArray();
        if (!renamed.Select(t => t.Text).SequenceEqual(reparsed.Select(t => t.Text)))
        {
            int mismatch = Enumerable.Range(0, Math.Min(renamed.Length, reparsed.Length)).FirstOrDefault(i => renamed[i].Text != reparsed[i].Text);
            throw new Exception($"Token round-trip changed token {mismatch}: {renamed[mismatch].Kind()} '{renamed[mismatch].Text}' -> {reparsed[mismatch].Kind()} '{reparsed[mismatch].Text}'. Context: {string.Join(" ", renamed.Skip(Math.Max(0,mismatch-4)).Take(9).Select(t=>t.Text))}");
        }
        var version = program.Members.OfType<FieldDeclarationSyntax>().SelectMany(f => f.Declaration.Variables)
            .Single(v => v.Identifier.ValueText == "ControllerBuildVersion").Initializer!.Value.ToString();
        // Keep setting declarations, section headings and inline notes; detailed prose lives in the source.
        var settings = string.Join("\n", source[..settingsEnd].Replace("\r\n", "\n").Split('\n').Where(line =>
            !line.TrimStart().StartsWith("//") || line.Contains("USER SETTINGS") ||
            System.Text.RegularExpressions.Regex.IsMatch(line.TrimStart(), @"^// [A-E][23]?\.")));
        bool automaticArm = source.Contains("// ===== IMPLEMENTATION =====", StringComparison.Ordinal);
        var packed = (automaticArm ? "// AutoArm v" : "// MArmOS mixed mining arm v") + version.Trim('"') + " - paste this ENTIRE file into the programmable block.\n" +
            "// Starts OFF. Stop before replacing; Check, then On. No collision avoidance.\n" +
            "// Settings remain editable. Full source: " + Path.GetFileName(tree.FilePath) + ".\n" + settings +
            "// Generated implementation: edit the readable source and run tools/" + (automaticArm ? "Build-AutoArm.ps1" : "Build.ps1") + ".\n" + compact + "\n";
        if (automaticArm) packed = "// AutoArm " + version.Trim('"') + "\n// OFF. Check/On. No collision avoidance.\n" + settings + compact + "\n";
        var packedCompilation = Compile(packed, output);
        Check(packedCompilation);
        CompareIL(compilation, packedCompilation);
        // Reverse the exact token substitutions and compare to the complete source token stream.
        for (int i = 0; i < tokens.Length; i++)
        {
            var expected = byToken.TryGetValue(tokens[i].SpanStart, out var s) && symbols.TryGetValue(s, out var n) ? n : tokens[i].Text;
            if (reparsed[i].Text != expected) throw new Exception("Identifier round-trip failed.");
        }
        if (packed.Length > 100000) throw new Exception($"Packed script still exceeds 100,000 characters: {packed.Length}.");
        File.WriteAllText(output, packed, new UTF8Encoding(false));
        var mapPath = Path.Combine(Path.GetDirectoryName(output)!, "tools", "ScriptPack", "obj", Path.GetFileNameWithoutExtension(output) + ".names.json");
        Directory.CreateDirectory(Path.GetDirectoryName(mapPath)!);
        File.WriteAllText(mapPath, JsonSerializer.Serialize(symbols.Select(kv => new { symbol = kv.Key.ToDisplayString(), name = kv.Value }), new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Packed: {packed.Length:N0} characters; {100000 - packed.Length:N0} free to 100,000. Token round-trip, compilation and identical method IL: PASS. Renamed {symbols.Count} symbols.");
    }

    static void CompareIL(CSharpCompilation source, CSharpCompilation packed)
    {
        using var a = new MemoryStream();
        using var b = new MemoryStream();
        if (!source.Emit(a).Success || !packed.Emit(b).Success) throw new Exception("Cannot compare failed compilation.");
        a.Position = b.Position = 0;
        using var pa = new PEReader(a);
        using var pb = new PEReader(b);
        var ma = pa.GetMetadataReader();
        var mb = pb.GetMetadataReader();
        if (ma.MethodDefinitions.Count != mb.MethodDefinitions.Count) throw new Exception("Packing changed method count.");
        foreach (var pair in ma.MethodDefinitions.Zip(mb.MethodDefinitions))
        {
            var x = ma.GetMethodDefinition(pair.First);
            var y = mb.GetMethodDefinition(pair.Second);
            if (x.Attributes != y.Attributes || !ma.GetBlobBytes(x.Signature).SequenceEqual(mb.GetBlobBytes(y.Signature))) throw new Exception("Packing changed method signature.");
            if (x.RelativeVirtualAddress == 0 && y.RelativeVirtualAddress == 0) continue;
            var ix = pa.GetMethodBody(x.RelativeVirtualAddress);
            var iy = pb.GetMethodBody(y.RelativeVirtualAddress);
            if (!ix.GetILBytes()!.SequenceEqual(iy.GetILBytes()!) || ix.MaxStack != iy.MaxStack ||
                !ix.ExceptionRegions.SequenceEqual(iy.ExceptionRegions) || ix.LocalVariablesInitialized != iy.LocalVariablesInitialized ||
                ix.LocalSignature != iy.LocalSignature)
                throw new Exception("Packing changed executable method body: " + ma.GetString(x.Name));
        }
        // IL string operands are heap offsets; also require the string contents to match.
        for (int i = 1; i < ma.GetHeapSize(System.Reflection.Metadata.Ecma335.HeapIndex.UserString);)
        {
            var handle = System.Reflection.Metadata.Ecma335.MetadataTokens.UserStringHandle(i);
            if (ma.GetUserString(handle) != mb.GetUserString(handle)) throw new Exception("Packing changed a string literal.");
            var next = ma.GetNextHandle(handle);
            if (next.IsNil) break;
            i = System.Reflection.Metadata.Ecma335.MetadataTokens.GetHeapOffset(next);
        }
    }

    static string ShortName(int index)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var name = "";
        do { name = alphabet[index % alphabet.Length] + name; index = index / alphabet.Length - 1; } while (index >= 0);
        return name;
    }

    static string Compact(SyntaxToken[] tokens)
    {
        var b = new StringBuilder();
        string previous = "";
        int lineStart = 0;
        foreach (var token in tokens)
        {
            string text = token.Text;
            if (previous.Length > 0)
            {
                var pair = SyntaxFactory.ParseTokens(previous + text, options: ParseOptions).Where(t => !t.IsKind(SyntaxKind.EndOfFileToken)).ToArray();
                if (pair.Length != 2 || pair[0].Text != previous || pair[1].Text != text) b.Append(' ');
            }
            b.Append(text);
            if (b.Length - lineStart >= 180 && (text == ";" || text == "}")) { b.Append('\n'); lineStart = b.Length; }
            previous = text;
        }
        return b.ToString();
    }
}
