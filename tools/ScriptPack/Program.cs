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
        var originalCompilation = compilation;
        source = ShortDeclarations(source, compilation);
        compilation = Compile(source, "declaration compression");
        Check(compilation);
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
            if (s is IParameterSymbol parameter && parameter.ContainingSymbol is IMethodSymbol { AssociatedSymbol: IPropertySymbol indexer } &&
                parameter.Ordinal < indexer.Parameters.Length) s = indexer.Parameters[parameter.Ordinal];
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
            if (symbol == null || symbol.IsImplicitlyDeclared || protectedNames.Contains(symbol.Name) || symbol.Name.Length <= 1 ||
                !symbol.Locations.Any(l => l.IsInSource) || symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum } ||
                symbol is IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum }) continue;
            if (symbol is not (IFieldSymbol or IMethodSymbol or IPropertySymbol or INamedTypeSymbol or ILocalSymbol or IParameterSymbol)) continue;
            byToken[token.SpanStart] = symbol;
        }
        var taken = root.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Select(t => t.ValueText).ToHashSet(StringComparer.Ordinal);
        int next = 0;
        foreach (var group in byToken.Values.Where(s => s is not (ILocalSymbol or IParameterSymbol))
            .GroupBy(s => s, SymbolEqualityComparer.Default).OrderByDescending(g => g.Count() * (g.Key!.Name.Length - 1)))
        {
            string name;
            do { name = ShortName(next++); } while (taken.Contains(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None);
            if (name.Length >= group.Key!.Name.Length) continue;
            taken.Add(name);
            symbols[group.Key] = name;
        }
        // A local alias can be reused in separate methods, but never in sibling
        // or nested lambdas belonging to the same outer method. Reserve every
        // original identifier and every member alias to prevent new capture or
        // shadowing, including references to unqualified fields and methods.
        ISymbol LocalScope(ISymbol symbol)
        {
            ISymbol scope = symbol.ContainingSymbol;
            for (var owner = scope; owner != null && owner is not INamedTypeSymbol; owner = owner.ContainingSymbol)
                if (owner is IMethodSymbol) scope = owner;
            return scope;
        }
        foreach (var scope in byToken.Values.Where(s => s is ILocalSymbol or IParameterSymbol)
            .Distinct(SymbolEqualityComparer.Default).GroupBy(s => LocalScope(s), SymbolEqualityComparer.Default)
            .OrderBy(g => g.Key is IPropertySymbol ? 0 : 1))
        {
            var localTaken = new HashSet<string>(taken, StringComparer.Ordinal);
            // Indexer parameters belong to the property symbol, but are in
            // scope in both accessors. Allocate those first, then reserve their
            // aliases in each accessor's independent method scope.
            if (scope.Key is IMethodSymbol { AssociatedSymbol: IPropertySymbol property })
                foreach (var parameter in property.Parameters)
                    if (symbols.TryGetValue(parameter, out var alias)) localTaken.Add(alias);
            int localNext = 0;
            foreach (var group in byToken.Values.Where(s => s is ILocalSymbol or IParameterSymbol &&
                SymbolEqualityComparer.Default.Equals(LocalScope(s), scope.Key))
                .GroupBy(s => s, SymbolEqualityComparer.Default).OrderByDescending(g => g.Count() * (g.Key!.Name.Length - 1)))
            {
                string name;
                do { name = LocalName(localNext++); } while (localTaken.Contains(name) ||
                    SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None);
                if (name.Length >= group.Key!.Name.Length) continue;
                localTaken.Add(name);
                symbols[group.Key] = name;
            }
        }
        var tokens = program.DescendantTokens().Where(t => t.SpanStart >= boundary && t != program.CloseBraceToken && t.Span.Length > 0).ToArray();
        var literalReplacements = new Dictionary<int, string>();
        var renamed = tokens.Select(t =>
        {
            if (byToken.TryGetValue(t.SpanStart, out var s) && symbols.TryGetValue(s, out var n)) return SyntaxFactory.Identifier(n);
            var literal = ShortNumber(t) ?? ShortString(t);
            if (literal == null) return t.WithoutTrivia();
            literalReplacements.Add(t.SpanStart, literal);
            return SyntaxFactory.ParseTokens(literal, options: ParseOptions).First();
        }).ToArray();
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
        CompareIL(originalCompilation, packedCompilation);
        // Reverse the exact token substitutions and compare to the complete source token stream.
        for (int i = 0; i < tokens.Length; i++)
        {
            var restored = reparsed[i].Text;
            string? replacement = byToken.TryGetValue(tokens[i].SpanStart, out var s) && symbols.TryGetValue(s, out var n) ? n :
                literalReplacements.GetValueOrDefault(tokens[i].SpanStart);
            if (replacement != null)
            {
                if (restored != replacement) throw new Exception("Substitution round-trip failed.");
                restored = tokens[i].Text;
            }
            if (restored != tokens[i].Text) throw new Exception("Source token round-trip failed.");
        }
        if (packed.Length > 100000) throw new Exception($"Packed script still exceeds 100,000 characters: {packed.Length}.");
        File.WriteAllText(output, packed, new UTF8Encoding(false));
        var mapPath = Path.Combine(Path.GetDirectoryName(output)!, "tools", "ScriptPack", "obj", Path.GetFileNameWithoutExtension(output) + ".names.json");
        Directory.CreateDirectory(Path.GetDirectoryName(mapPath)!);
        File.WriteAllText(mapPath, JsonSerializer.Serialize(symbols.Select(kv => new { symbol = kv.Key.ToDisplayString(), name = kv.Value }), new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Packed: {packed.Length:N0} characters; {100000 - packed.Length:N0} free to 100,000. Token round-trip, compilation and identical method IL: PASS. Renamed {symbols.Count} symbols.");
    }

    static string ShortDeclarations(string source, CSharpCompilation compilation)
    {
        int boundary = source.IndexOf("static string MiningTuningError", StringComparison.Ordinal);
        if (boundary < 0) boundary = source.IndexOf("// ===== IMPLEMENTATION =====", StringComparison.Ordinal);
        if (boundary < 0) throw new Exception("Missing settings boundary.");
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var edits = new List<(int Start, int Length, string Text)>();
        foreach (var array in tree.GetRoot().DescendantNodes().OfType<ArrayCreationExpressionSyntax>())
        {
            if (array.SpanStart < Header.Length + boundary || array.Initializer == null ||
                array.Type.RankSpecifiers.Count != 1 || array.Type.RankSpecifiers[0].Rank != 1 ||
                array.Type.RankSpecifiers[0].Sizes.Any(s => s is not OmittedArraySizeExpressionSyntax)) continue;
            var inferredSyntax = SyntaxFactory.ParseExpression("new[]" + array.Initializer.ToString(), options: ParseOptions);
            var originalType = model.GetTypeInfo(array).Type;
            var inferredType = model.GetSpeculativeTypeInfo(array.SpanStart, inferredSyntax, SpeculativeBindingOption.BindAsExpression).Type;
            if (originalType == null || !SymbolEqualityComparer.Default.Equals(originalType, inferredType)) continue;
            edits.Add((array.Type.SpanStart - Header.Length, array.Type.Span.Length, "[]"));
        }
        foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            var variables = declaration.Declaration;
            if (variables.Type.SpanStart < Header.Length + boundary || variables.Type.IsVar ||
                variables.Type.Span.Length <= 3 || variables.Variables.Count != 1 ||
                declaration.Modifiers.Count != 0) continue;
            var initializer = variables.Variables[0].Initializer?.Value;
            if (initializer == null) continue;
            var declared = model.GetTypeInfo(variables.Type).Type;
            var inferred = model.GetTypeInfo(initializer).Type;
            // Script-owned simple type names receive one-character aliases later;
            // replacing those with the three-character keyword would cost space.
            if (variables.Type is IdentifierNameSyntax && declared?.Locations.Any(l => l.IsInSource) == true) continue;
            // Equality excludes implicit numeric/reference conversions and target-typed
            // null or lambda initializers. The original-source IL gate below also
            // rejects any unforeseen overload or emitted-local change.
            if (declared == null || !SymbolEqualityComparer.Default.Equals(declared, inferred)) continue;
            edits.Add((variables.Type.SpanStart - Header.Length, variables.Type.Span.Length, "var"));
        }
        foreach (var token in tree.GetRoot().DescendantTokens().Where(t => t.IsKind(SyntaxKind.PrivateKeyword)))
        {
            if (token.SpanStart < Header.Length + boundary || token.Parent?.Parent is not ClassDeclarationSyntax) continue;
            edits.Add((token.SpanStart - Header.Length, token.Span.Length, ""));
        }
        var result = new StringBuilder(source);
        foreach (var edit in edits.OrderByDescending(e => e.Start))
            result.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text);
        return result.ToString();
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

    static readonly string MemberAlphabet = BuildMemberAlphabet();
    static string BuildMemberAlphabet()
    {
        var alphabet = new StringBuilder("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
        // Keep the Greek local-alias pool separate. These BMP letters each
        // occupy one UTF-16 code unit and remain valid C# 6 identifier starts.
        foreach (var range in new[] { (First: 0x0100, Last: 0x02AF), (First: 0x0400, Last: 0x04FF) })
            for (int code = range.First; code <= range.Last; code++)
            {
                char letter = (char)code;
                if (char.IsLetter(letter) && SyntaxFacts.IsIdentifierStartCharacter(letter)) alphabet.Append(letter);
            }
        return alphabet.ToString();
    }

    static string ShortName(int index)
    {
        string alphabet = MemberAlphabet;
        var name = "";
        do { name = alphabet[index % alphabet.Length] + name; index = index / alphabet.Length - 1; } while (index >= 0);
        return name;
    }

    static string LocalName(int index)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZαβγδεζηθικλμνξοπρστυφχψωΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩ";
        var name = "";
        do { name = alphabet[index % alphabet.Length] + name; index = index / alphabet.Length - 1; } while (index >= 0);
        return name;
    }

    static string? ShortNumber(SyntaxToken token)
    {
        if (!token.IsKind(SyntaxKind.NumericLiteralToken)) return null;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string[] candidates;
        string suffix;
        if (token.Value is double number) { candidates = new[] { number.ToString("R", culture), number.ToString("E16", culture) }; suffix = "d"; }
        else if (token.Value is float single) { candidates = new[] { single.ToString("R", culture), single.ToString("E8", culture) }; suffix = "f"; }
        else return null; // Integer and decimal literal types are unchanged.
        string best = token.Text;
        foreach (var text in candidates)
        {
            int exponentAt = text.IndexOfAny(new[] { 'E', 'e' });
            string mantissa = exponentAt < 0 ? text : text[..exponentAt];
            if (mantissa.Contains('.')) mantissa = mantissa.TrimEnd('0').TrimEnd('.');
            if (mantissa.StartsWith("0.", StringComparison.Ordinal)) mantissa = mantissa[1..];
            string candidate = mantissa;
            if (exponentAt >= 0)
            {
                int exponent = int.Parse(text[(exponentAt + 1)..], culture);
                if (exponent != 0) candidate += "e" + exponent.ToString(culture);
            }
            if (suffix == "f" || !candidate.Contains('.') && !candidate.Contains('e')) candidate += suffix;
            if (candidate.Length >= best.Length) continue;
            var parsed = SyntaxFactory.ParseTokens(candidate, options: ParseOptions).First();
            if (parsed.IsKind(SyntaxKind.NumericLiteralToken) && parsed.Value?.GetType() == token.Value?.GetType() && Equals(parsed.Value, token.Value)) best = candidate;
        }
        return best == token.Text ? null : best;
    }

    static string? ShortString(SyntaxToken token)
    {
        if (!token.IsKind(SyntaxKind.StringLiteralToken)) return null;
        string value = token.ValueText;
        // Verbatim strings retain literal newlines and tabs. Keep other control
        // characters escaped so the generated file remains safe to copy/paste.
        if (value.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')) return null;
        string candidate = "@\"" + value.Replace("\"", "\"\"") + "\"";
        if (candidate.Length >= token.Text.Length) return null;
        var parsed = SyntaxFactory.ParseTokens(candidate, options: ParseOptions).ToArray();
        return parsed.Length == 2 && parsed[0].IsKind(SyntaxKind.StringLiteralToken) &&
            parsed[0].ValueText == value ? candidate : null;
    }

    static string Compact(SyntaxToken[] tokens)
    {
        var b = new StringBuilder();
        string previous = "";
        foreach (var token in tokens)
        {
            string text = token.Text;
            if (previous.Length > 0)
            {
                var pair = SyntaxFactory.ParseTokens(previous + text, options: ParseOptions).Where(t => !t.IsKind(SyntaxKind.EndOfFileToken)).ToArray();
                if (pair.Length != 2 || pair[0].Text != previous || pair[1].Text != text) b.Append(' ');
            }
            b.Append(text);
            previous = text;
        }
        return b.ToString();
    }
}

