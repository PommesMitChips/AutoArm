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
        if(aliases.Report.Any(r=>r.Kind=="namespace type"&&r.Accepted&&r.Symbol.StartsWith("System.Collections",StringComparison.Ordinal)))throw new Exception("Collection aliases bypass SE memory-safety rewriting.");
        SyntaxCases();
        Console.WriteLine("Compression regressions: PASS (cost rejection, escaped strings, conversions, overloads, nested calls, side effects, null receivers, namespace aliases and nameof).");
    }
    static void SyntaxCases()
    {
        string body="public Program(){}public void Main(string a,UpdateType b){}public void Save(){}"+
            "struct V{int n;public int Next(){return ++n;}}readonly V Value;readonly object Ref=new object();static readonly object StaticRef=new object();"+
            "const string Version=\"a deliberately long shared version\";class C{const string Other=\"a deliberately long shared version\";public static int Count(){return Other.Length;}}"+
            "class D{const string Named=\"a deliberately long shared version\";public static string Name(){return nameof(Named);}}"+
            "public static long Probe(){int x=0;{x++;}if((x==1)){{x+=C.Count();}}if(x>0){if(x==0)x=9;else x++;}else{x=0;}"+
            "int y=0;{int y2=2; y+=y2;}return x+y+Version.Length+D.Name().Length;}public int ValueProbe(){return Value.Next()+Value.Next();}";
        var reduced=SyntaxCompression.Run(body);
        if(reduced.Source.Length>=body.Length||Execute(body)!=Execute(reduced.Source)||!reduced.Report.Any(r=>r.Symbol=="Private equal string constants"&&r.Accepted))throw new Exception("Syntax compression changed guarded scopes, branches, constants or nameof, or failed to consolidate constants.");
        if(!reduced.Source.Contains("readonly V Value")||!reduced.Source.Contains("static readonly object StaticRef")||reduced.Source.Contains("readonly object Ref"))throw new Exception("Readonly value/static/reference policy failed.");
        ScriptPack.CompareIL(ScriptPack.Compile("public Program(){}readonly System.Collections.Generic.List<int> Values=new System.Collections.Generic.List<int>();public void Main(string a,UpdateType b){}public void Save(){}","readonly reference control"),ScriptPack.Compile(SyntaxCompression.Run("public Program(){}readonly System.Collections.Generic.List<int> Values=new System.Collections.Generic.List<int>();public void Main(string a,UpdateType b){}public void Save(){}").Source,"readonly reference output"));
        string shadow="public Program(){}public void Main(string a,UpdateType b){}public void Save(){} }namespace N{using X=global::System.StringComparer;class System{public class StringComparer{}}class C{public X x=null;}";
        if(!SyntaxCompression.Run(shadow).Source.Contains("global::System.StringComparer"))throw new Exception("Relative alias captured a shadow namespace/type.");
        Console.WriteLine("Syntax regressions: PASS (scope/declaration guards, dangling else, readonly defensive copies, alias shadowing and bound constants/nameof).");
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
