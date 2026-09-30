using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class Regression
{
    const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    static int assertions;
    internal static void Run(string baseline, string source)
    {
        var oldRoot = ScriptPack.Compile(File.ReadAllText(baseline), baseline).SyntaxTrees.Single().GetRoot();
        var newRoot = ScriptPack.Compile(File.ReadAllText(source), source).SyntaxTrees.Single().GetRoot();
        CheckUnchanged(oldRoot, newRoot);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(ScriptPack.GameBin, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var oldType = Probe(oldRoot, false);
        var newType = Probe(newRoot, true);
        Cholesky(oldType, newType);
        Axis(oldType, newType);
        Legacy(oldType, newType);
        Topology(oldType, newType);
        Console.WriteLine($"Regression: PASS ({assertions:N0} assertions; seed 7292026).");
    }
    static MethodDeclarationSyntax Method(SyntaxNode root, string name) => root.DescendantNodes().OfType<MethodDeclarationSyntax>().Last(m => m.Identifier.ValueText == name);
    static string Tokens(SyntaxNode node) => string.Join(" ", node.DescendantTokens().Where(t => t.Span.Length > 0).Select(t => t.Text));
    static void Assert(bool ok, string message)
    {
        assertions++;
        if (!ok) throw new Exception(message);
    }
    static void CheckUnchanged(SyntaxNode oldRoot, SyntaxNode newRoot)
    {
        var allowed = new HashSet<string> { "MiningAxisVector", "HeadCholesky", "MArmOS_Main", "ControllerBuildBanner", "DescribeControllerBuild", "Main", "AxisToDirection", "RecordHeadTrackingTrace", "SolveHeadVelocity", "MixedTopology", "RelMove", "ExecuteCommand" };
        string Key(MethodDeclarationSyntax m) => m.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.Text + "." + m.Identifier.Text;
        var current = newRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().ToDictionary(Key);
        int count = 0;
        foreach (var method in oldRoot.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (allowed.Contains(method.Identifier.Text)) continue;
            Assert(current.TryGetValue(Key(method), out var other) && Tokens(method) == Tokens(other), "Unexpected method change: " + Key(method));
            count++;
        }
        string FieldKey(VariableDeclaratorSyntax v) => v.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.Text + "." + v.Identifier.Text;
        var currentFields = newRoot.DescendantNodes().OfType<FieldDeclarationSyntax>().SelectMany(f => f.Declaration.Variables).ToDictionary(FieldKey);
        foreach (var field in oldRoot.DescendantNodes().OfType<FieldDeclarationSyntax>().SelectMany(f => f.Declaration.Variables))
        {
            if (field.Identifier.Text is "ControllerBuildVersion" or "Override") continue;
            Assert(currentFields.TryGetValue(FieldKey(field), out var other) && Tokens(field) == Tokens(other), "Setting/state initializer changed: " + FieldKey(field));
        }
        var oldSolver = Method(oldRoot, "SolveHeadVelocity").ToString().Replace("HeadGram[r, c] = HeadGram[c, r] = total", "HeadGram[r, c] = total");
        Assert(Tokens(SyntaxFactory.ParseMemberDeclaration(oldSolver)!) == Tokens(Method(newRoot, "SolveHeadVelocity")), "Unexpected bounded solver change.");
        Console.WriteLine($"Preservation: {count} unchanged method token streams; existing configuration, calibration and state initializers match.");
    }
    static Type Probe(SyntaxNode root, bool revised)
    {
        string Methods(params string[] names) => string.Join("\n", names.Select(n => Method(root, n).ToString()));
        string axis = revised ? "" : Method(root, "AxisToDirection").WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.StaticKeyword))).NormalizeWhitespace().ToFullString();
        string legacy;
        if (revised) legacy = Method(root, "ExecuteLegacyMove").WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.StaticKeyword))).NormalizeWhitespace().ToFullString();
        else
        {
            var cases = Method(root, "ExecuteCommand").DescendantNodes().OfType<SwitchSectionSyntax>()
                .Where(s => s.Labels.OfType<CaseSwitchLabelSyntax>().Any(l => l.Value.ToString() is "\"move\"" or "\"moveto\"" or "\"relmove\""));
            legacy = "static void ExecuteLegacyMove(string[] Words,int mode){double X=0,Y=0,Z=0,Yaw=0,Pitch=0,Roll=0;switch(mode==0?\"move\":mode==1?\"moveto\":\"relmove\"){" + string.Join("\n", cases.Select(c => c.ToString())) + "}}";
        }
        // Exercise the exact source grid-role check with supplied fake grid IDs.
        var statements = Method(root, "MixedTopology").Body!.Statements;
        int start = Enumerable.Range(0, statements.Count).First(i => statements[i].ToString().StartsWith(revised ? "TopologyRoles.Clear" : "var roles ="));
        int end = Enumerable.Range(start, statements.Count - start).First(i => statements[i] is ForEachStatementSyntax f && f.Expression.ToString() == "MiningDrives");
        var roleCheck = "static bool Roles(long[] ids){GridIds=ids;string why;" + string.Join("\n", statements.Skip(start).Take(end-start)) + "return true;}";
        string text = "using System;using System.Collections.Generic;using System.Globalization;using VRageMath;public class Probe{" +
            Methods("MiningFinite", "HeadCholesky", "MiningAxisVector", "MixedParse", "STD") + axis + legacy + roleCheck +
            """
            static bool HeadMode,UseTarget;
            static double[] Result;
            static void Move(double x,double y,double z,double yaw,double pitch,double roll){Result=new[]{0d,x,y,z,yaw,pitch,roll};}
            static void MoveTo(double x,double y,double z,double yaw,double pitch,double roll){Result=new[]{1d,x,y,z,yaw,pitch,roll};}
            static void RelMove(double x,double y,double z,double yaw,double pitch,double roll){Result=new[]{2d,x,y,z,yaw,pitch,roll};}
            public static string Legacy(string[] words,int mode,bool head){HeadMode=head;UseTarget=false;Result=null;try{ExecuteLegacyMove(words,mode);return UseTarget+"|"+string.Join(",",Array.ConvertAll(Result,v=>v.ToString("R",CultureInfo.InvariantCulture)));}catch(Exception e){return e.GetType().Name+"|"+e.Message+"|"+UseTarget;}}
            static HashSet<long> TopologyRoles=new HashSet<long>();
            static long[] GridIds;
            class IMyCubeGrid{public long EntityId;public IMyCubeGrid(long id){EntityId=id;}}
            class Block{public IMyCubeGrid CubeGrid,TopGrid;}
            static Block MB(int i,int j=0){int index=i<3?i+1:i+2;if(i==2&&j==1)index=4;return new Block{CubeGrid=new IMyCubeGrid(GridIds[0]),TopGrid=new IMyCubeGrid(GridIds[index])};}
            }
            """;
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).Append(MetadataReference.CreateFromFile(Path.Combine(ScriptPack.GameBin, "VRage.Math.dll")));
        var compilation = CSharpCompilation.Create("Regression_" + Guid.NewGuid().ToString("N"), new[] { CSharpSyntaxTree.ParseText(text, ScriptPack.ParseOptions) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
        return Assembly.Load(stream.ToArray()).GetType("Probe")!;
    }
    static void Cholesky(Type a, Type b)
    {
        var random = new Random(7292026);
        var ma = a.GetMethod("HeadCholesky", Static)!;
        var mb = b.GetMethod("HeadCholesky", Static)!;
        var la = new double[6,6]; var lb = new double[6,6];
        for (int test = 0; test < 5008; test++)
        {
            var m = new double[6,6]; var gram = new double[6,6]; var rhs = new double[6];
            for(int i=0;i<6;i++) { rhs[i]=random.NextDouble()*2-1; for(int j=0;j<6;j++)m[i,j]=random.NextDouble()*2-1; }
            for(int i=0;i<6;i++)for(int j=0;j<6;j++){double v=i==j?0.000004:0;for(int k=0;k<6;k++)v+=m[i,k]*m[j,k];gram[i,j]=v;}
            if(test==5000)gram=new double[6,6];
            if(test==5001)gram[0,0]=-1;
            if(test==5002)gram[0,0]=double.NaN;
            if(test==5003)gram[2,2]=double.PositiveInfinity;
            if(test==5004)gram[5,5]=double.NegativeInfinity;
            if(test==5005)gram[0,0]=1e-16;
            if(test==5006)rhs[3]=double.NaN;
            if(test==5007)rhs[0]=double.PositiveInfinity;
            // Dirty all scratch buffers: earlier failures must not poison a later solve.
            for(int i=0;i<6;i++)for(int j=0;j<6;j++)la[i,j]=lb[i,j]=double.NaN;
            var ta=Enumerable.Repeat(double.NaN,6).ToArray();var tb=(double[])ta.Clone();
            var xa=(double[])ta.Clone();var xb=(double[])ta.Clone();
            bool ra=(bool)ma.Invoke(null,new object[]{gram,rhs,la,ta,xa})!;
            var lower=(double[,])gram.Clone();for(int i=0;i<6;i++)for(int j=i+1;j<6;j++)lower[i,j]=double.NaN;
            bool rb=(bool)mb.Invoke(null,new object[]{lower,rhs,lb,tb,xb})!;
            Assert(ra==rb,"Cholesky success/failure mismatch.");
            if(!ra)continue;
            for(int i=0;i<6;i++)Assert(BitConverter.DoubleToInt64Bits(xa[i])==BitConverter.DoubleToInt64Bits(xb[i]),"Cholesky output changed.");
            for(int i=0;i<6;i++){double v=-rhs[i];for(int j=0;j<6;j++)v+=gram[i,j]*xb[j];Assert(Math.Abs(v)<1e-8,"Cholesky residual too large.");}
        }
        Console.WriteLine("Cholesky: 5,000 positive-definite systems + 8 hostile cases; bit-identical outputs with dirty/unused matrix entries.");
    }
    static void Axis(Type a, Type b)
    {
        foreach(var axis in new string?[]{"X","Y","Z","-X","-Y","-Z","","x","-Q",null})
        {
            var oldMining=a.GetMethod("MiningAxisVector",Static)!.Invoke(null,new object?[]{axis});
            var newMining=b.GetMethod("MiningAxisVector",Static)!.Invoke(null,new object?[]{axis,false});
            Assert(Equals(oldMining,newMining),"Mining axis changed.");
            var oldLinear=a.GetMethod("AxisToDirection",Static)!.Invoke(null,new object?[]{axis});
            var newLinear=b.GetMethod("MiningAxisVector",Static)!.Invoke(null,new object?[]{axis,true});
            Assert(Equals(oldLinear,newLinear),"Linear axis fallback changed.");
        }
        Console.WriteLine("Axis mapping: six axes and unknown/null fallbacks match.");
    }
    static void Legacy(Type a, Type b)
    {
        var tests=new List<string[]>();
        for(int n=1;n<=8;n++)tests.Add(Enumerable.Range(0,n).Select(i=>i==0?"command":(i*1.25).ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        foreach(var invalid in new[]{"broken","NaN","Infinity","1,5","1e999"})for(int n=1;n<=6;n++)
        {var words=new[]{"command","1","2","3","4","5","6"};words[n]=invalid;tests.Add(words);}
        int count=0;
        foreach(var culture in new[]{"en-US","es-ES"})
        {
            var previous=System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo(culture);
                foreach(var words in tests)for(int mode=0;mode<3;mode++)foreach(var head in new[]{false,true})
                {
                    var x=a.GetMethod("Legacy")!.Invoke(null,new object[]{words,mode,head});
                    var y=b.GetMethod("Legacy")!.Invoke(null,new object[]{words,mode,head});
                    Assert(Equals(x,y),$"Legacy command changed (mode={mode}, head={head}, words={string.Join(' ',words)}): '{x}' vs '{y}'.");count++;
                }
            }
            finally{System.Globalization.CultureInfo.CurrentCulture=previous;}
        }
        Console.WriteLine($"Legacy commands: {count} full/partial/invalid argument, mode and culture combinations match.");
    }
    static void Topology(Type a, Type b)
    {
        var ma=a.GetMethod("Roles",Static)!;var mb=b.GetMethod("Roles",Static)!;
        var ids=Enumerable.Range(1,14).Select(i=>(long)i).ToArray();
        Assert((bool)mb.Invoke(null,new object[]{ids})!,"Distinct grid roles rejected.");
        for(int i=0;i<14;i++)for(int j=0;j<i;j++)
        {
            var duplicate=(long[])ids.Clone();duplicate[i]=duplicate[j];
            Assert(Equals(ma.Invoke(null,new object[]{duplicate}),mb.Invoke(null,new object[]{duplicate})),"Grid role duplicate changed.");
            Assert((bool)mb.Invoke(null,new object[]{ids})!,"Roles not reset after failed topology check.");
        }
        Console.WriteLine("Topology roles: all 91 duplicate pairs and recovery on the next call match.");
    }
}
