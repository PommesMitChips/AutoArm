using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Only generated syntax is changed. Readable sources retain their contracts.
internal static class SyntaxCompression
{
    internal sealed record Result(string Source,List<RepetitionCompression.Decision> Report);
    static string Body(SyntaxNode root){int header=SyntaxFactory.ParseTokens(ScriptPack.Header,options:ScriptPack.ParseOptions).Count(t=>!t.IsKind(SyntaxKind.EndOfFileToken));var tokens=root.DescendantTokens().Where(t=>!t.IsKind(SyntaxKind.EndOfFileToken)).Skip(header).SkipLast(1).ToArray();return tokens[0].LeadingTrivia.ToFullString()+ScriptPack.Compact(tokens)+"\n";}
    static string Identity(ISymbol? symbol)=>symbol==null?"":symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)+" / "+symbol.ContainingAssembly?.Identity;
    static bool Good(CSharpCompilation c)=>!c.GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error);
    internal static Result Run(string source,bool blockBodies=false)
    {
        var report=new List<RepetitionCompression.Decision>();
        var compilation=ScriptPack.Compile(source,"syntax compression");
        var tree=compilation.SyntaxTrees.Single();var model=compilation.GetSemanticModel(tree);
        var visitor=new Reduce(model,blockBodies);var trial=Body(visitor.Visit(tree.GetRoot())!);
        var changed=ScriptPack.Compile(trial,"scope/parentheses/modifier validation");
        ScriptPack.Check(changed);ScriptPack.CompareIL(compilation,changed);
        report.Add(new("syntax","Scopes, parentheses and private reference modifiers",visitor.Count,source.Length-trial.Length,true,"C#6 compilation and executable IL are unchanged. Readonly value/static fields and sealed classes retained."));
        source=trial;compilation=changed;tree=compilation.SyntaxTrees.Single();model=compilation.GetSemanticModel(tree);

        // A relative alias can bind another valid type after global:: is removed.
        // Rebind every alias and retain the qualifier for any changed target.
        var aliases=tree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u=>u.Alias!=null&&u.Name!.ToString().StartsWith("global::",StringComparison.Ordinal)).ToArray();
        var before=aliases.ToDictionary(u=>u.Alias!.Name.Identifier.ValueText,u=>Identity(((IAliasSymbol)model.GetDeclaredSymbol(u)!).Target));
        var remove=new HashSet<string>(before.Keys);var rewritten=new Aliases(remove).Visit(tree.GetRoot())!;
        var aliasTrial=ScriptPack.Compile(Body(rewritten),"relative alias binding");
        var aliasModel=aliasTrial.GetSemanticModel(aliasTrial.SyntaxTrees.Single());
        foreach(var u in aliasTrial.SyntaxTrees.Single().GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u=>u.Alias!=null))
            if(before.TryGetValue(u.Alias!.Name.Identifier.ValueText,out var expected)&&Identity((aliasModel.GetDeclaredSymbol(u) as IAliasSymbol)?.Target)!=expected)remove.Remove(u.Alias.Name.Identifier.ValueText);
        trial=Body(new Aliases(remove).Visit(tree.GetRoot())!);changed=ScriptPack.Compile(trial,"relative alias validation");
        if(Good(changed)){ScriptPack.CompareIL(compilation,changed);report.Add(new("syntax","Resolved global namespace aliases",remove.Count,source.Length-trial.Length,true,"Every alias binds to its original symbol/assembly."));source=trial;compilation=changed;}

        tree=compilation.SyntaxTrees.Single();model=compilation.GetSemanticModel(tree);
        var fields=tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Select(v=>(node:v,symbol:model.GetDeclaredSymbol(v) as IFieldSymbol))
            .Where(p=>p.symbol is {IsConst:true,DeclaredAccessibility:Accessibility.Private,Type.SpecialType:SpecialType.System_String,ConstantValue:string}&&p.node.Parent!.Parent is FieldDeclarationSyntax {AttributeLists.Count:0}).ToArray();
        var redirects=new Dictionary<IFieldSymbol,IFieldSymbol>(SymbolEqualityComparer.Default);int acceptedLength=source.Length;
        foreach(var duplicate in fields){
            var field=duplicate.symbol!;
            // nameof is an observable identifier, not a value reference.
            var uses=tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Where(n=>SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(n).Symbol,field)).ToArray();
            if(uses.Any(n=>n.Ancestors().OfType<InvocationExpressionSyntax>().Any(i=>i.Expression.ToString()=="nameof")))continue;
            var canonical=fields.Select(f=>f.symbol!).FirstOrDefault(f=>!SymbolEqualityComparer.Default.Equals(f,field)&&Equals(f.ConstantValue,field.ConstantValue)&&Ancestor(f.ContainingType,field.ContainingType)&&(!SymbolEqualityComparer.Default.Equals(f.ContainingType,field.ContainingType)||f.Locations[0].SourceSpan.Start<field.Locations[0].SourceSpan.Start));
            if(canonical==null||redirects.ContainsKey(canonical))continue;
            var one=new Dictionary<IFieldSymbol,IFieldSymbol>(redirects,SymbolEqualityComparer.Default){[field]=canonical};
            trial=Body(new Constants(model,one).Visit(tree.GetRoot())!);changed=ScriptPack.Compile(trial,"constant value validation");
            if(trial.Length>=acceptedLength||!Good(changed))continue;
            // Compare every remaining constant use's compile-time type/value.
            // Field-token indices may shift when a declaration is removed;
            // this is a semantic transformation, not raw IL-token renaming.
            var newTree=changed.SyntaxTrees.Single();var newModel=changed.GetSemanticModel(newTree);
            var original=CanonicalUses(tree.GetRoot(),model,one);
            var after=CanonicalUses(newTree.GetRoot(),newModel,new Dictionary<IFieldSymbol,IFieldSymbol>(SymbolEqualityComparer.Default));
            if(!original.SequenceEqual(after))continue;
            redirects=one;acceptedLength=trial.Length;
        }
        if(redirects.Count>0){trial=Body(new Constants(model,redirects).Visit(tree.GetRoot())!);ScriptPack.Check(ScriptPack.Compile(trial,"deduplicated constants"));report.Add(new("syntax","Private equal string constants",redirects.Count,source.Length-trial.Length,true,"Bound string uses retain constant type/value; ancestor accessibility and nameof guarded."));source=trial;}
        return new(source,report);
    }
    static IEnumerable<string> CanonicalUses(SyntaxNode root,SemanticModel model,Dictionary<IFieldSymbol,IFieldSymbol> redirects)
    {
        foreach(var n in root.DescendantNodes().OfType<ExpressionSyntax>().Where(n=>n is IdentifierNameSyntax||n is MemberAccessExpressionSyntax)){
            if(n.Ancestors().OfType<FieldDeclarationSyntax>().Any(f=>f.Modifiers.Any(SyntaxKind.ConstKeyword)))continue;
            if(n.Parent is MemberAccessExpressionSyntax access&&access.Name==n)continue;
            if(model.GetSymbolInfo(n).Symbol is not IFieldSymbol {IsConst:true} f)continue;
            if(redirects.TryGetValue(f,out var replacement))f=replacement;
            yield return f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)+" / "+(f.Type.Locations.Any(l=>l.IsInSource)?"script":f.Type.ContainingAssembly?.Identity.ToString())+"="+f.ConstantValue;
        }
    }
    static bool Ancestor(INamedTypeSymbol outer,INamedTypeSymbol inner){for(var t=inner;t!=null;t=t.ContainingType)if(SymbolEqualityComparer.Default.Equals(t,outer))return true;return false;}
    sealed class Constants(SemanticModel model,Dictionary<IFieldSymbol,IFieldSymbol> redirects):CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax n){var removed=n.Declaration.Variables.Where(v=>redirects.ContainsKey((IFieldSymbol)model.GetDeclaredSymbol(v)!)).Select(v=>v.Identifier.ValueText).ToHashSet();var rewritten=(FieldDeclarationSyntax)base.VisitFieldDeclaration(n)!;var remaining=rewritten.Declaration.Variables.Where(v=>!removed.Contains(v.Identifier.ValueText)).ToArray();return remaining.Length==0?null:rewritten.WithDeclaration(rewritten.Declaration.WithVariables(SyntaxFactory.SeparatedList(remaining)));}
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax n)=>model.GetSymbolInfo(n).Symbol is IFieldSymbol f&&redirects.TryGetValue(f,out var c)?SyntaxFactory.ParseExpression(c.ContainingType.Name+"."+c.Name).WithTriviaFrom(n):base.VisitIdentifierName(n);
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax n)=>model.GetSymbolInfo(n).Symbol is IFieldSymbol f&&redirects.TryGetValue(f,out var c)?SyntaxFactory.ParseExpression(c.ContainingType.Name+"."+c.Name).WithTriviaFrom(n):base.VisitMemberAccessExpression(n);
    }
    sealed class Aliases(HashSet<string> remove):CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax n)=>n.Alias!=null&&remove.Contains(n.Alias.Name.Identifier.ValueText)?n.WithName(SyntaxFactory.ParseName(n.Name!.ToString()[8..])):n;
    }
    sealed class Reduce(SemanticModel model,bool blockBodies):CSharpSyntaxRewriter
    {
        public int Count;
        static bool ScopeFree(BlockSyntax block)=>!block.DescendantNodes().Any(n=>n is VariableDeclaratorSyntax or ForEachStatementSyntax or CatchDeclarationSyntax or LabeledStatementSyntax or GotoStatementSyntax);
        public override SyntaxNode? VisitBlock(BlockSyntax n){
            var block=(BlockSyntax)base.VisitBlock(n)!;
            var list=new List<StatementSyntax>();foreach(var s in block.Statements){if(s is BlockSyntax b&&ScopeFree(b)){Count++;list.AddRange(b.Statements);}else list.Add(s);}
            block=block.WithStatements(SyntaxFactory.List(list));
            if(block.Statements.Count==1&&block.Statements[0] is ExpressionStatementSyntax or ReturnStatementSyntax or ThrowStatementSyntax or BreakStatementSyntax or ContinueStatementSyntax&&n.Parent is IfStatementSyntax or ElseClauseSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or UsingStatementSyntax or LockStatementSyntax){Count++;return block.Statements[0].WithTriviaFrom(n);}
            return block;
        }
        public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax n){
            var p=(ParenthesizedExpressionSyntax)base.VisitParenthesizedExpression(n)!;
            if(p.Expression is ParenthesizedExpressionSyntax or IdentifierNameSyntax or LiteralExpressionSyntax or InvocationExpressionSyntax or MemberAccessExpressionSyntax or ElementAccessExpressionSyntax or ThisExpressionSyntax||n.Parent is IfStatementSyntax i&&i.Condition==n||n.Parent is WhileStatementSyntax w&&w.Condition==n||n.Parent is DoStatementSyntax d&&d.Condition==n||n.Parent is ForStatementSyntax f&&f.Condition==n){Count++;return p.Expression.WithTriviaFrom(n);}return p;
        }
        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax n){
            var field=(FieldDeclarationSyntax)base.VisitFieldDeclaration(n)!;
            if(n.SpanStart>=ScriptPack.Header.Length&&n.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)&&!n.Modifiers.Any(SyntaxKind.StaticKeyword)&&n.AttributeLists.Count==0&&n.Declaration.Variables.All(v=>model.GetDeclaredSymbol(v) is IFieldSymbol {DeclaredAccessibility:Accessibility.Private,Type.IsReferenceType:true})){Count++;field=field.WithModifiers(SyntaxFactory.TokenList(field.Modifiers.Where(t=>!t.IsKind(SyntaxKind.ReadOnlyKeyword))));}return field;
        }
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax n){var type=(ClassDeclarationSyntax)base.VisitClassDeclaration(n)!;if(n.SpanStart>=ScriptPack.Header.Length&&n.Parent is NamespaceDeclarationSyntax&&n.Modifiers.Any(SyntaxKind.PublicKeyword)){Count++;return type.WithModifiers(SyntaxFactory.TokenList(type.Modifiers.Where(t=>!t.IsKind(SyntaxKind.PublicKeyword))));}return type;}
        // C#6 accepts these, but the installed resource visitor can leave a
        // semicolon on its replacement block. Its normalized text then fails
        // reparsing. Keep lambdas; expand declaration expression bodies.
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax n){var m=(MethodDeclarationSyntax)base.VisitMethodDeclaration(n)!;if(!blockBodies||m.ExpressionBody==null)return m;StatementSyntax s=m.ReturnType.ToString()=="void"?SyntaxFactory.ExpressionStatement(m.ExpressionBody.Expression):SyntaxFactory.ReturnStatement(m.ExpressionBody.Expression);return m.WithExpressionBody(null).WithSemicolonToken(default).WithBody(SyntaxFactory.Block(s));}
        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax n){var p=(PropertyDeclarationSyntax)base.VisitPropertyDeclaration(n)!;if(!blockBodies||p.ExpressionBody==null)return p;var get=SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(p.ExpressionBody.Expression)));return p.WithExpressionBody(null).WithSemicolonToken(default).WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(get)));}
    }
}
