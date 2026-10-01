using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class CompressionChecks
{
    internal static void Run()
    {
        string input="public Program(){}public void Main(string a,UpdateType b){}public void Save(){}"+
            "static int Counter;static double Next(){return ++Counter;}"+
            "static string Text(){Counter++;return \"a long repeated string with \\\"quotes\\\" and \\n newlines\";}"+
            "public static long Probe(){long sum=0;var ini=new MyIni();ini.Set(\"a long repeated section name\",\"number\",42L);";
        for(int i=0;i<15;i++)input+="sum+=ini.Get(\"a long repeated section name\",\"number\").ToInt64();sum+=(long)Math.Max(Next(),Next());sum+=Text().Length;";
        input+="sum+=(long)Math.Max(Math.Max(Next(),Next()),Next());string a=\"x\",b=\"x\";try{string missing=null;sum+=missing.Length;}catch(NullReferenceException){sum+=7;}return sum+Counter+a.Length+b.Length;}";
        var compressed=RepetitionCompression.Run(input,0);
        if(compressed.Source.Length>=input.Length || !compressed.Report.Any(r=>r.Kind=="string"&&r.Accepted) ||
            !compressed.Report.Any(r=>r.Kind=="method"&&r.Symbol.Contains("ToInt64")&&r.Accepted) ||
            !compressed.Report.Any(r=>r.Kind=="string"&&r.Symbol=="x"&&!r.Accepted))throw new Exception("Compression cost-selection fixture failed.");
        AssemblyLoadContext.Default.Resolving+=Resolve;
        try { if(Execute(input)!=Execute(compressed.Source))throw new Exception("Compression changed nested calls, argument side effects, null behavior, conversions or literal values."); }
        finally { AssemblyLoadContext.Default.Resolving-=Resolve; }
        string types="public Program(){}public void Main(string a,UpdateType b){}public void Save(){}public static string Names(){return nameof(IMyProgrammableBlock)+nameof(Program);}";
        for(int i=0;i<80;i++)types+=$"static IMyMechanicalConnectionBlock Block{i}=null;static Vector3D Pose{i}=Vector3D.Zero;static System.Collections.Generic.List<IMyProgrammableBlock> Peers{i}=null;";
        var aliases=NamespaceCompression.Run(types);
        if(aliases.Source.Length>=types.Length || !aliases.Report.Any(r=>r.Kind=="namespace layout"&&r.Accepted))throw new Exception("Namespace alias overhead/nameof/IL fixture failed.");
        Console.WriteLine("Compression regressions: PASS (cost rejection, escaped strings, conversions, overloads, nested calls, side effects, null receivers, namespace aliases and nameof).");
    }
    static Assembly? Resolve(AssemblyLoadContext context,AssemblyName name)
    {
        string path=Path.Combine(ScriptPack.GameBin,name.Name+".dll");
        return File.Exists(path)?context.LoadFromAssemblyPath(path):null;
    }
    static long Execute(string source)
    {
        var tree=CSharpSyntaxTree.ParseText(ScriptPack.Header.Replace(" : MyGridProgram","")+source+"}",ScriptPack.ParseOptions);
        var compilation=CSharpCompilation.Create("CompressionCheck_"+Guid.NewGuid().ToString("N"),new[]{tree},ScriptPack.Compile("","references").References,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release));
        using var stream=new MemoryStream();
        if(!compilation.Emit(stream).Success)throw new Exception("Compression execution fixture failed compilation.");
        return (long)Assembly.Load(stream.ToArray()).GetType("Program")!.GetMethod("Probe")!.Invoke(null,null)!;
    }
}
