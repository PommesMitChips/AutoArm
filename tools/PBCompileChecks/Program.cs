using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class Program
{
    const string Header = "using System;using System.Collections.Generic;using System.Linq;using System.Text;using Sandbox.ModAPI.Ingame;using Sandbox.ModAPI.Interfaces;using SpaceEngineers.Game.ModAPI.Ingame;using VRage.Game;using VRage.Game.ModAPI.Ingame;using VRage.Game.ModAPI.Ingame.Utilities;using VRageMath;public class Program:MyGridProgram{";
    static readonly string GameBin = Environment.GetEnvironmentVariable("SE_BIN") ?? @"C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64";
    static readonly CSharpParseOptions Options = new(LanguageVersion.CSharp6);

    static void Main(string[] args)
    {
        try
        {
            AssemblyLoadContext.Default.Resolving += Resolve;
            var assembly = Assembly.LoadFrom(Path.Combine(GameBin, "VRage.Scripting.dll"));
            var rewriter = assembly.GetType("VRage.Scripting.Rewriters.TypeSafetyAndBlockRewriter", true)!;
            var framework = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\Framework64\v4.0.30319");
            var references = new[] { "mscorlib.dll", "System.dll", "System.Core.dll", "System.Xml.dll" }.Select(p => Path.Combine(framework, p))
                .Concat(new[] { "netstandard.dll", "Sandbox.Common.dll", "Sandbox.Game.dll", "SpaceEngineers.Game.dll", "VRage.dll", "VRage.Game.dll", "VRage.Library.dll", "VRage.Math.dll", "VRage.Scripting.dll" }.Select(p => Path.Combine(GameBin, p)))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToArray();
            Regression(rewriter, references);
            foreach (string path in args)
            {
                var result = Rewrite(File.ReadAllText(path), path, rewriter, references);
                RequireCompilation(result.Compilation);
                Console.WriteLine($"Installed SE type-safety/memory-safe rewrite + C#6: PASS ({Path.GetFileName(path)})");
            }
            if (args.Length == 0) throw new Exception("Supply PB script paths to validate.");
        }
        catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
    }

    static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        string path = Path.Combine(GameBin, name.Name + ".dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    }

    static (CSharpCompilation Compilation, string Text) Rewrite(string source, string label, Type rewriter, MetadataReference[] references)
    {
        var tree = CSharpSyntaxTree.ParseText(Header + source + "}", Options, label);
        var compilation = CSharpCompilation.Create("Rewrite_" + Guid.NewGuid().ToString("N"), new[] { tree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
        RequireCompilation(compilation);
        var visitor = (CSharpSyntaxRewriter)Activator.CreateInstance(rewriter, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { compilation.GetSemanticModel(tree), tree, true }, null)!;
        var rewritten = CSharpSyntaxTree.Create((CSharpSyntaxNode)visitor.Visit(tree.GetRoot())!, Options, label);
        return (compilation.ReplaceSyntaxTree(tree, rewritten), rewritten.ToString());
    }

    static void RequireCompilation(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (result.Success) return;
        throw new Exception(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(15)));
    }

    static void Regression(Type rewriter, MetadataReference[] references)
    {
        const string entry = "public Program(){}public void Main(string a,UpdateType b){}public void Save(){}";
        const string plain = "readonly List<int> list=new List<int>();readonly Dictionary<string,List<int>> dictionary=new Dictionary<string,List<int>>();readonly HashSet<int> set=new HashSet<int>();readonly Queue<int> queue=new Queue<int>();readonly StringBuilder text=new StringBuilder();";
        var control = Rewrite(entry + plain, "unaliased memory-safe controls", rewriter, references);
        RequireCompilation(control.Compilation);
        foreach (string name in new[] { "List", "Dictionary", "HashSet", "Queue", "StringBuilder" })
            if (!control.Text.Contains("MemorySafe" + name, StringComparison.Ordinal)) throw new Exception("Installed rewrite did not instrument " + name);
        var broken = Rewrite(entry + "}namespace N{using À=global::System.Text.StringBuilder;public class C{readonly À text=new À();}", "bad StringBuilder alias", rewriter, references);
        if (!broken.Compilation.GetDiagnostics().Any(d => d.Id == "CS0234" && d.GetMessage().Contains("MemorySafeÀ", StringComparison.Ordinal)))
            throw new Exception("The failing StringBuilder alias no longer reproduces; revisit the compatibility restriction.");
        var bypass = Rewrite(entry + "}namespace N{using À=global::System.Collections.Generic.List<int>;public class C{readonly À list=new À();}", "closed collection alias", rewriter, references);
        RequireCompilation(bypass.Compilation);
        if (bypass.Text.Contains("MemorySafeList", StringComparison.Ordinal))
            throw new Exception("The closed collection alias no longer bypasses rewriting; revisit the compatibility restriction.");
        Console.WriteLine("Installed SE rewrite regressions: PASS (original alias error, silent collection bypass, unaliased instrumentation).");
    }
}
