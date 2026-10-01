using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class NamespaceCompression
{
    internal sealed record Result(string Source, List<RepetitionCompression.Decision> Report);
    static string Compact(string source) => ScriptPack.Compact(SyntaxFactory.ParseTokens(source, options: ScriptPack.ParseOptions).Where(t=>!t.IsKind(SyntaxKind.EndOfFileToken)).ToArray());
    static string Compact(SyntaxNode node) => ScriptPack.Compact(node.DescendantTokens().ToArray());
    static bool Unavailable(ITypeSymbol type) => type is ITypeParameterSymbol || type.Locations.Any(l=>l.IsInSource) || type is INamedTypeSymbol named && named.TypeArguments.Any(Unavailable) || type is IArrayTypeSymbol array && Unavailable(array.ElementType);
    internal static Result Run(string source)
    {
        var compilation=ScriptPack.Compile(source,"namespace alias discovery");
        var tree=compilation.SyntaxTrees.Single();
        var root=tree.GetRoot();
        var model=compilation.GetSemanticModel(tree);
        var program=root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var owner=model.GetDeclaredSymbol(program)!;
        var used=root.DescendantTokens().Where(t=>t.IsKind(SyntaxKind.IdentifierToken)).Select(t=>t.ValueText).ToHashSet();
        int next=0x1E00;
        string Fresh() { while(next<0xFFEF) { char c=(char)next++; if(char.IsLetter(c)&&SyntaxFacts.IsIdentifierStartCharacter(c)&&used.Add(c.ToString()))return c.ToString(); } throw new Exception("Identifier pool exhausted."); }
        string ns=Fresh(), implementation=Fresh(), host=Fresh(), instance=Fresh();
        var bridges=new Dictionary<string,string>();
        foreach(string name in new[]{"Me","GridTerminalSystem","Runtime","IGC","Storage","Echo"})bridges[name]=Fresh();
        var aliases=new Dictionary<INamedTypeSymbol,string>(SymbolEqualityComparer.Default);
        var report=new List<RepetitionCompression.Decision>();
        string measuredBody=Compact(new Rename(model,owner,aliases,implementation).Visit(program)!);
        foreach(var group in program.DescendantNodes().OfType<SimpleNameSyntax>()
            .Select(n=>(node:n,symbol:model.GetSymbolInfo(n).Symbol as INamedTypeSymbol))
            .Where(x=>x.symbol!=null && !Unavailable(x.symbol) && x.node.ToString().Length>1)
            .GroupBy(x=>x.symbol!,SymbolEqualityComparer.Default))
        {
            var type=(INamedTypeSymbol)group.Key!;
            string alias=Fresh();
            string declaration=Compact($"using {alias}={type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions & ~SymbolDisplayMiscellaneousOptions.UseSpecialTypes))};");
            var proposed=new Dictionary<INamedTypeSymbol,string>(aliases,SymbolEqualityComparer.Default) {[type]=alias};
            string proposedBody=Compact(new Rename(model,owner,proposed,implementation).Visit(program)!);
            int saving=measuredBody.Length-proposedBody.Length-declaration.Length;
            bool accept=saving>0;
            report.Add(new("namespace type",type.ToDisplayString(),group.Count(),saving,accept,accept?"Alias declaration is smaller than repeated type tokens.":"Alias declaration costs at least as much as the repeated tokens."));
            if(accept) { aliases[type]=alias; measuredBody=proposedBody; }
        }
        if(aliases.Count==0)return new(source,report);
        var constructors=program.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if(constructors.Length!=1 || constructors[0].ParameterList.Parameters.Count!=0 || constructors[0].Initializer!=null)
            throw new Exception("Namespace compression requires the standard parameterless PB constructor.");
        var rewritten=(ClassDeclarationSyntax)new Rename(model,owner,aliases,implementation).Visit(program)!;
        var constructor=rewritten.Members.OfType<ConstructorDeclarationSyntax>().Single();
        constructor=constructor.WithParameterList(SyntaxFactory.ParseParameterList($"(global::Program {host})"))
            .WithBody(constructor.Body!.WithStatements(constructor.Body.Statements.Insert(0,SyntaxFactory.ParseStatement($"this.{host}={host};"))));
        rewritten=rewritten.ReplaceNode(rewritten.Members.OfType<ConstructorDeclarationSyntax>().Single(),constructor);
        string helperBody=ScriptPack.Compact(rewritten.Members.SelectMany(m=>m.DescendantTokens()).ToArray());
        string BridgeType(string name)=>name switch {
            "Me"=>"IMyProgrammableBlock", "GridTerminalSystem"=>"IMyGridTerminalSystem", "Runtime"=>"IMyGridProgramRuntimeInfo", "IGC"=>"IMyIntergridCommunicationSystem", _=>"string" };
        string wrapper=$"readonly {ns}.{implementation} {instance};public Program(){{{instance}=new {ns}.{implementation}(this);}}public void Main(string a,UpdateType b){{{instance}.Main(a,b);}}public void Save(){{{instance}.Save();}}";
        string facade=$"readonly global::Program {host};";
        foreach(string name in new[]{"Me","GridTerminalSystem","Runtime","IGC"})
        {
            wrapper+=$"public {BridgeType(name)} {bridges[name]}=>{name};";
            facade+=$"{BridgeType(name)} {name}=>{host}.{bridges[name]};";
        }
        wrapper+=$"public string {bridges["Storage"]}{{get{{return Storage;}}set{{Storage=value;}}}}public void {bridges["Echo"]}(string a){{Echo(a);}}";
        facade+=$"string Storage{{get{{return {host}.{bridges["Storage"]};}}set{{{host}.{bridges["Storage"]}=value;}}}}void Echo(string a){{{host}.{bridges["Echo"]}(a);}}";
        string usingText=string.Concat(aliases.Select(p=>$"using {p.Value}={p.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions & ~SymbolDisplayMiscellaneousOptions.UseSpecialTypes))};"));
        string prefix=source[..(program.Members.First().GetFirstToken().SpanStart-ScriptPack.Header.Length)];
        string trial=prefix+Compact(wrapper+"}namespace "+ns+"{"+usingText+$"public class {implementation}{{"+facade+helperBody+"}");
        var trialCompilation=ScriptPack.Compile(trial,"namespace alias validation");
        var errors=trialCompilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
        int total=source.Length-trial.Length;
        if(errors.Length>0 || total<=0)
        {
            report.Add(new("namespace layout","Forwarding PB entry point",1,total,false,errors.Length>0?string.Join(";",errors.Take(3).Select(e=>e.ToString())):"Forwarding overhead exceeds the combined alias savings."));
            return new(source,report.Select(r=>r with {Accepted=false}).ToList());
        }
        ScriptPack.Check(trialCompilation);
        var trialTree=trialCompilation.SyntaxTrees.Single();
        var trialModel=trialCompilation.GetSemanticModel(trialTree);
        var helperClass=trialTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.ValueText==implementation);
        var helperSymbol=trialModel.GetDeclaredSymbol(helperClass)!;
        var originalMembers=helperClass.Members.Skip(7).ToArray();
        var restoredMembers=originalMembers.Select(m=>new Restore(trialModel,helperSymbol,aliases.ToDictionary(p=>p.Value,p=>p.Key)).Visit(m)!).ToArray();
        string restored=string.Concat(restoredMembers.Select(m=>m.ToFullString()));
        ScriptPack.CompareIL(compilation,ScriptPack.Compile(restored,"namespace alias expansion IL proof"));
        report.Add(new("namespace layout","Combined type aliases including forwarding overhead",1,total,true,"Full C# 6 compilation passed; total output is smaller."));
        Console.WriteLine($"Namespace aliases: {aliases.Count} types; {total:N0} characters saved including forwarding overhead.");
        return new(trial,report);
    }
    sealed class Restore(SemanticModel model,INamedTypeSymbol helper,Dictionary<string,INamedTypeSymbol> aliases) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if(aliases.TryGetValue(node.Identifier.ValueText,out var type))return SyntaxFactory.ParseName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions & ~SymbolDisplayMiscellaneousOptions.UseSpecialTypes))).WithTriviaFrom(node);
            if(model.GetSymbolInfo(node).Symbol is INamedTypeSymbol symbol && SymbolEqualityComparer.Default.Equals(symbol,helper))return SyntaxFactory.IdentifierName("Program").WithTriviaFrom(node);
            return base.VisitIdentifierName(node);
        }
        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var result=(ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
            if(node.Parent is not ClassDeclarationSyntax p || !SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(p),helper))return result;
            return result.WithIdentifier(SyntaxFactory.Identifier("Program")).WithParameterList(SyntaxFactory.ParameterList()).WithBody(result.Body!.WithStatements(result.Body.Statements.RemoveAt(0)));
        }
    }
    sealed class Rename(SemanticModel model, INamedTypeSymbol owner, Dictionary<INamedTypeSymbol,string> aliases,string implementation) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if(node.Expression is IdentifierNameSyntax name && name.Identifier.ValueText=="nameof" && model.GetConstantValue(node) is {HasValue:true,Value:string value})
                return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,SyntaxFactory.Literal(value)).WithTriviaFrom(node);
            return base.VisitInvocationExpression(node);
        }
        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node) => model.GetSymbolInfo(node).Symbol is INamedTypeSymbol symbol && aliases.TryGetValue(symbol,out var alias) ? SyntaxFactory.IdentifierName(alias).WithTriviaFrom(node) : base.VisitQualifiedName(node);
        public override SyntaxNode? VisitGenericName(GenericNameSyntax node) => model.GetSymbolInfo(node).Symbol is INamedTypeSymbol symbol && aliases.TryGetValue(symbol,out var alias) ? SyntaxFactory.IdentifierName(alias).WithTriviaFrom(node) : base.VisitGenericName(node);
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) => model.GetSymbolInfo(node).Symbol is INamedTypeSymbol symbol && aliases.TryGetValue(symbol,out var alias) ? SyntaxFactory.IdentifierName(alias).WithTriviaFrom(node) : base.VisitMemberAccessExpression(node);
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if(model.GetSymbolInfo(node).Symbol is INamedTypeSymbol symbol)
            {
                if(SymbolEqualityComparer.Default.Equals(symbol,owner))return SyntaxFactory.IdentifierName(implementation).WithTriviaFrom(node);
                if(aliases.TryGetValue(symbol,out var alias))return SyntaxFactory.IdentifierName(alias).WithTriviaFrom(node);
            }
            return base.VisitIdentifierName(node);
        }
        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var result=(ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
            return node.Parent is ClassDeclarationSyntax parent && SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(parent),owner)?result.WithIdentifier(SyntaxFactory.Identifier(implementation)):result;
        }
    }
}
