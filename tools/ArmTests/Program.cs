using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class Tests
{
    internal static int Assertions;
    internal static string Workspace = "";
    internal static string EngineSource = "", ToolEngineSource = "";
    internal static void Assert(bool condition, string message)
    {
        Assertions++;
        if (!condition) throw new Exception(message);
    }
    internal static readonly string GameBin = Environment.GetEnvironmentVariable("SE_BIN") ?? @"C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64";
    internal static Type Script(string source,bool counted=false)
    {
        const string header = "using System;using System.Collections.Generic;using System.Linq;using System.Text;using Sandbox.ModAPI.Ingame;using Sandbox.ModAPI.Interfaces;using SpaceEngineers.Game.ModAPI.Ingame;using VRage.Game;using VRage.Game.ModAPI.Ingame;using VRage.Game.ModAPI.Ingame.Utilities;using VRageMath;public class Program:TestHost{";
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).ToList();
        paths.Add(typeof(TestHost).Assembly.Location);
        paths.AddRange(new[]{"Sandbox.Common.dll","Sandbox.Game.dll","SpaceEngineers.Game.dll","VRage.dll","VRage.Game.dll","VRage.Library.dll","VRage.Math.dll","VRage.Scripting.dll"}.Select(p=>Path.Combine(GameBin,p)));
        // The host may already list copied game assemblies. Deduplicate by assembly name.
        var references = paths.GroupBy(Path.GetFileName).Select(g=>MetadataReference.CreateFromFile(g.First()));
        var tree = CSharpSyntaxTree.ParseText(header + source + "\n}", new CSharpParseOptions(LanguageVersion.CSharp6));
        var compilation = CSharpCompilation.Create("ArmTest_"+Guid.NewGuid().ToString("N"),new[]{tree},references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        if(counted)
        {
            var asm=Assembly.LoadFrom(Path.Combine(GameBin,"VRage.Scripting.dll"));
            var compiler=asm.GetType("VRage.Scripting.MyScriptCompiler",true)!;
            var rewriter=asm.GetType("VRage.Scripting.Rewriters.ResourceMonitoringRewriter",true)!;
            var visitor=(CSharpSyntaxRewriter)Activator.CreateInstance(rewriter,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,
                new object?[]{compiler.GetField("Static",BindingFlags.Public|BindingFlags.Static)!.GetValue(null),compilation,tree,true},null)!;
            var rewritten=CSharpSyntaxTree.Create((CSharpSyntaxNode)visitor.Visit(tree.GetRoot())!,new CSharpParseOptions(LanguageVersion.CSharp6));
            File.WriteAllText(Path.Combine(Workspace,"tools/ScriptPack/obj/collision-counted.cs"),rewritten.ToString());
            compilation=compilation.ReplaceSyntaxTree(tree,rewritten);
        }
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
            if(args.Length>0&&args[0]=="--model-types")
            {
                foreach(string dll in new[]{"VRage.Library.dll","VRage.Render.dll","VRage.Render11.dll","VRage.Game.dll"})
                {
                    var asm=Assembly.LoadFrom(Path.Combine(GameBin,dll));
                    Type[] types;try{types=asm.GetTypes();}catch(ReflectionTypeLoadException e){types=e.Types.Where(t=>t!=null).Cast<Type>().ToArray();}
                    foreach(var t in types.Where(t=>t.Name.Contains("ModelImporter")||t.Name.Contains("ModelReader")))
                    {Console.WriteLine(t.FullName+" / "+dll);foreach(var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))Console.WriteLine(m);}
                }
                return 0;
            }
            if(args.Length==1&&args[0]=="--counting-types")
            {
                var asm=Assembly.LoadFrom(Path.Combine(GameBin,"VRage.Scripting.dll"));
                foreach(var t in asm.GetTypes().Where(t=>t.Name.Contains("Instruction")||t.Name.Contains("Runtime")||t.FullName!.Contains("Rewriter")))
                {
                    Console.WriteLine(t.FullName);
                    foreach(var c in t.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))Console.WriteLine("CTOR "+c);
                    foreach(var m in t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly))Console.WriteLine("METHOD "+m);
                    foreach(var f in t.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static))Console.WriteLine("FIELD "+f+" = "+(f.IsLiteral?f.GetRawConstantValue():null));
                    if(t.Name=="ResourceMonitoringRewriter")Console.WriteLine("INJECTION "+t.GetMethod("InstructionCounterCall",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null));
                }
                var compiler=asm.GetType("VRage.Scripting.MyScriptCompiler",true)!;
                foreach(var c in compiler.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))Console.WriteLine("COMPILER CTOR "+c);
                foreach(var f in compiler.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))Console.WriteLine("COMPILER FIELD "+f);
                var injector=Assembly.LoadFrom(Path.Combine(GameBin,"VRage.Library.dll")).GetType("VRage.Library.Compiler.IlInjector",true)!;
                foreach(var m in injector.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))Console.WriteLine("INJECTOR "+m);
                foreach(var p in injector.GetProperties(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))Console.WriteLine("INJECTOR PROPERTY "+p);
                var handle=injector.GetMethod("BeginRunBlock")!.ReturnType;
                foreach(var p in handle.GetProperties())Console.WriteLine("HANDLE PROPERTY "+p);
                foreach(var m in handle.GetMethods())Console.WriteLine("HANDLE METHOD "+m);
                return 0;
            }
            if(args.Length>1&&args[0]=="--model-bounds")
            {
                var importerType=Assembly.LoadFrom(Path.Combine(GameBin,"VRage.Render.dll")).GetType("VRageRender.Import.MyModelImporter",true)!;
                var importer=Activator.CreateInstance(importerType)!;
                foreach(string file in args.Skip(1))
                {
                    if(!File.Exists(file))throw new FileNotFoundException("Model asset not found.",file);
                    importerType.GetMethod("ImportData")!.Invoke(importer,new object?[]{file,new[]{"BoundingBox","BoundingSphere","Dummies"}});
                    Console.WriteLine(file);
                    foreach(System.Collections.DictionaryEntry tag in (System.Collections.IDictionary)importerType.GetMethod("GetTagData")!.Invoke(importer,null)!)Console.WriteLine(tag.Key+" = "+tag.Value);
                    importerType.GetMethod("Clear")!.Invoke(importer,null);
                }
                return 0;
            }
            if(args.Length==2&&args[1]=="--control-audit")
            { Scenarios.ControlAudit(Script(File.ReadAllText(args[0]))); return 0; }
            if(args.Length==3&&args[2]=="--control-compare")
            { Scenarios.ControlCompare(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(args[1]))); return 0; }
            if(args.Length==2&&args[1]=="--servo-compare")
            { string source=File.ReadAllText(args[0]); Scenarios.ServoComparison(Script(source),Script(Scenarios.ServoCandidate(source))); return 0; }
            if(args.Length==2&&args[1]=="--packed-hosts")
            {
                Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
                Scenarios.RunHosts(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_ToolSwap_Source.txt"))));
                CheckPackedPrograms();
                return 0;
            }
            if((args.Length==2||args.Length==3)&&args[1]=="--collision")
            { Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;Scenarios.CollisionCases(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(args.Length==3?args[2]:Path.Combine(Workspace,"AutoArm_Collision_Source.txt"))));return 0; }
            if(args.Length==2&&args[1]=="--self-pairs")
            {
                Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;var script=Script(File.ReadAllText(args[0]));
                if(Path.GetFileName(args[0]).EndsWith("_Compact.txt"))PackedProgramChecks.Register(script,Path.Combine(Workspace,"tools/ScriptPack/obj/"+Path.GetFileNameWithoutExtension(args[0])+".names.json"));
                Scenarios.SelfPairCases(script);return 0;
            }
            if(args.Length==2&&args[1]=="--survey")
            { Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;Scenarios.SurveyCases(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_Survey.txt"))));return 0; }
            if(args.Length==2&&args[1]=="--bench")
            { Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;Scenarios.BenchCases(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_Bench.txt"))));return 0; }
            if(args.Length==3&&args[1]=="--survey-geometry")
            { Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;Scenarios.SurveyGeometryCases(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_Collision_Source.txt"))),args[2]);return 0; }
            if(args.Length==3&&args[1]=="--survey-encounters")
            { Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;Scenarios.SurveyGeometryCases(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_Collision_Source.txt"))),args[2],encounters:true);return 0; }
            if((args.Length==3||args.Length==4)&&args[1]=="--collision-profile")
            {
                Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;string path=args.Length==4?args[3]:Path.Combine(Workspace,"AutoArm_Collision_Source.txt");
                var collision=Script(File.ReadAllText(path),true);
                if(Path.GetFileName(path).EndsWith("_Compact.txt"))PackedProgramChecks.Register(collision,Path.Combine(Workspace,"tools/ScriptPack/obj/"+Path.GetFileNameWithoutExtension(path)+".names.json"));
                Scenarios.SurveyGeometryCases(Script(File.ReadAllText(args[0])),collision,args[2],true);return 0;
            }
            if(args.Length==5&&args[1]=="--self-profile")
            {
                Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;var collision=Script(File.ReadAllText(args[3]),true);
                if(Path.GetFileName(args[3]).EndsWith("_Compact.txt"))PackedProgramChecks.Register(collision,Path.Combine(Workspace,"tools/ScriptPack/obj/"+Path.GetFileNameWithoutExtension(args[3])+".names.json"));
                Scenarios.SurveyGeometryCases(Script(File.ReadAllText(args[0])),collision,args[2],true,selfMode:args[4]);return 0;
            }
            if(args.Length!=1) throw new Exception("Usage: ArmTests <script.txt> [--control-audit|--servo-compare]");
            Workspace=Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
            EngineSource=File.ReadAllText(Path.Combine(Workspace,"tools/ScriptPack/obj/AutoArm.Engine.txt"));
            ToolEngineSource=File.ReadAllText(Path.Combine(Workspace,"tools/ScriptPack/obj/AutoArm.ToolEngine.txt"));
            var type=Script(EngineSource);
            Console.WriteLine("Full script compiles against installed interfaces in the simulated PB host.");
            Scenarios.RunAll(type);
            Scenarios.WeightRepresentationCases(type);
            Scenarios.BenchCases(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_Bench.txt"))));
            Scenarios.RunHosts(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_ToolSwap_Source.txt"))));
            CheckPackedPrograms();
            Scenarios.CollisionCases(Script(File.ReadAllText(args[0])),Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_Collision_Source.txt"))));
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void CheckPackedPrograms()
    {
            var packedArm=Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_Compact.txt")));
            var packedTool=Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_ToolSwap_Compact.txt")));
            PackedProgramChecks.Register(packedArm,Path.Combine(Workspace,"tools/ScriptPack/obj/AutoArm_Compact.names.json"));
            PackedProgramChecks.Register(packedTool,Path.Combine(Workspace,"tools/ScriptPack/obj/AutoArm_ToolSwap_Compact.names.json"));
            Scenarios.RunHosts(packedArm,packedTool);
            var packedCollision=Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_Collision_Compact.txt")));
            PackedProgramChecks.Register(packedCollision,Path.Combine(Workspace,"tools/ScriptPack/obj/AutoArm_Collision_Compact.names.json"));
            Scenarios.CollisionCases(packedArm,packedCollision);
            Scenarios.BenchCases(packedArm,Script(File.ReadAllText(Path.Combine(Workspace,"AutoArm_Bench.txt"))));
            Console.WriteLine("Actual compressed programs: PASS (namespace entry points, aliases, strings and forwarding helpers).");
    }
}
