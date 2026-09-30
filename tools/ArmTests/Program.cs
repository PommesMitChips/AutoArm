using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class Tests
{
    internal static int Assertions;
    internal static string Workspace = "";
    internal static void Assert(bool condition, string message)
    {
        Assertions++;
        if (!condition) throw new Exception(message);
    }
    internal static readonly string GameBin = Environment.GetEnvironmentVariable("SE_BIN") ?? @"C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64";
    internal static Type Script(string source)
    {
        const string header = "using System;using System.Collections.Generic;using System.Linq;using System.Text;using Sandbox.ModAPI.Ingame;using Sandbox.ModAPI.Interfaces;using SpaceEngineers.Game.ModAPI.Ingame;using VRage.Game;using VRage.Game.ModAPI.Ingame;using VRage.Game.ModAPI.Ingame.Utilities;using VRageMath;public class Program:TestHost{";
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).ToList();
        paths.Add(typeof(TestHost).Assembly.Location);
        paths.AddRange(new[]{"Sandbox.Common.dll","Sandbox.Game.dll","SpaceEngineers.Game.dll","VRage.dll","VRage.Game.dll","VRage.Library.dll","VRage.Math.dll"}.Select(p=>Path.Combine(GameBin,p)));
        // The host may already list copied game assemblies. Deduplicate by assembly name.
        var references = paths.GroupBy(Path.GetFileName).Select(g=>MetadataReference.CreateFromFile(g.First()));
        var tree = CSharpSyntaxTree.ParseText(header + source + "\n}", new CSharpParseOptions(LanguageVersion.CSharp6));
        var compilation = CSharpCompilation.Create("ArmTest_"+Guid.NewGuid().ToString("N"),new[]{tree},references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
        return Assembly.Load(stream.ToArray()).GetType("Program")!;
    }
    internal static object Create(Type type, Rig rig)
    {
        TestHost.Next = rig;
        return Activator.CreateInstance(type)!;
    }
    internal static object? Field(object instance,string name) => instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(instance);
    internal static object? Call(object instance,string name,params object?[] args) => instance.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(instance,args);
    static int Main(string[] args)
    {
        try
        {
            AssemblyLoadContext.Default.Resolving += (_,name) =>
            {
                var path=Path.Combine(GameBin,name.Name+".dll");
                return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(path):null;
            };
            if(args.Length!=1) throw new Exception("Usage: ArmTests <script.txt>");
            Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
            var type=Script(File.ReadAllText(args[0]));
            Console.WriteLine("Full script compiles against installed interfaces in the simulated PB host.");
            Scenarios.RunAll(type);
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
