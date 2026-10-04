using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Runs after ordinary packing: the lengths below are actual UTF-16 output lengths,
// including the complete forwarding declaration, not occurrence-count estimates.
internal static class RepetitionCompression
{
    internal sealed record Decision(string Kind, string Symbol, int Occurrences, int Saved, bool Accepted, string Reason);
    internal sealed record Result(string Source, List<Decision> Report);
    sealed record Candidate(string Kind, string Key, List<SyntaxNode> Nodes, string Declaration, string Alias);
    sealed record Undo(string Alias, Dictionary<string, Queue<string>> Originals);
    static string Tokens(SyntaxNode node) => string.Join(" ", node.DescendantTokens().Select(t => t.Text));
    static string Minify(string text) => ScriptPack.Compact(SyntaxFactory.ParseTokens(text, options: ScriptPack.ParseOptions).Where(t => !t.IsKind(SyntaxKind.EndOfFileToken)).ToArray());
    static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    static bool UnsupportedType(ITypeSymbol t) => t is ITypeParameterSymbol || t.Locations.Any(l => l.IsInSource) ||
        t is INamedTypeSymbol n && n.TypeArguments.Any(UnsupportedType) || t is IArrayTypeSymbol a && UnsupportedType(a.ElementType);
    static string Key(ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    static bool Writable(MemberAccessExpressionSyntax n) => n.Parent is AssignmentExpressionSyntax a && a.Left == n ||
        n.Parent is PrefixUnaryExpressionSyntax || n.Parent is PostfixUnaryExpressionSyntax ||
        n.Parent is ArgumentSyntax arg && arg.RefKindKeyword.RawKind != 0;

    internal static Result Run(string baseline, int boundary)
    {
        string source = baseline;
        var report = new List<Decision>();
        var considered = new HashSet<string>();
        var helpers = new HashSet<string>();
        var undos = new List<Undo>();
        var used = SyntaxFactory.ParseTokens(source, options: ScriptPack.ParseOptions).Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Select(t => t.ValueText).ToHashSet();
        int next = 0x0500;
        string Fresh()
        {
            while (next < 0xFFEF)
            {
                char c = (char)next++;
                if (PortableIdentifiers.IsLetter(c) && used.Add(c.ToString())) return c.ToString();
            }
            throw new Exception("No unused single-character compression identifier.");
        }
        // Candidate keys are discovered once; each is rebound against the current
        // accepted program so earlier edits never invalidate positions or symbols.
        var initial = ScriptPack.Compile(source, "repetition discovery");
        var initialRoot = initial.SyntaxTrees.Single().GetRoot();
        var initialModel = initial.GetSemanticModel(initial.SyntaxTrees.Single());
        string probe = Fresh();
        var keys = Discover(initialRoot, initialModel, boundary, helpers, probe).Select(c => c.Kind + ":" + c.Key).Distinct().ToArray();
        foreach (string key in keys)
        {
            if (!considered.Add(key)) continue;
            var compilation = ScriptPack.Compile(source, "repetition candidate");
            var tree = compilation.SyntaxTrees.Single();
            var root = tree.GetRoot();
            var model = compilation.GetSemanticModel(tree);
            string alias = Fresh();
            var candidate = Discover(root, model, boundary, helpers, alias).FirstOrDefault(c => c.Kind + ":" + c.Key == key);
            if (candidate == null) continue;
            var rewriter = new Apply(candidate, model);
            var rewritten=rewriter.Visit(root)!;
            var program=rewritten.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            string trial = source[..boundary]+ScriptPack.Compact(program.DescendantTokens().Where(t=>t.SpanStart>=ScriptPack.Header.Length+boundary && t!=program.CloseBraceToken).ToArray())+"\n"+Minify(candidate.Declaration);
            int saved = source.Length - trial.Length;
            string reason = saved > 0 ? "" : "Declaration costs at least as much as the replaced text.";
            if (saved > 0)
            {
                var trialCompilation = ScriptPack.Compile(trial, "repetition validation");
                var error = trialCompilation.GetDiagnostics().FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
                if (error != null) reason = "Compilation refused: " + error.Id;
                else
                {
                    // Undo this one candidate before committing. This checks all
                    // affected syntax, including nested calls and literal escaping.
                    var undo = new Undo(alias, rewriter.Originals);
                    string expanded = Body(new Expand(undo).Visit(trialCompilation.SyntaxTrees.Single().GetRoot())!);
                    if (!TokenEqual(source, expanded)) reason = "Exact expansion round-trip refused.";
                }
            }
            bool accept = saved > 0 && reason.Length == 0;
            report.Add(new(candidate.Kind, candidate.Key, candidate.Nodes.Count, saved, accept, accept ? "Smaller final output; compilation and exact expansion passed." : reason));
            if (!accept) continue;
            // Expansion consumes queues; retain fresh copies for the final proof.
            undos.Add(new Undo(alias, Copy(rewriter.Originals)));
            helpers.Add(alias);
            source = trial;
        }
        string restored = source;
        for (int i = undos.Count - 1; i >= 0; i--)
        {
            var root = ScriptPack.Compile(restored, "expansion proof").SyntaxTrees.Single().GetRoot();
            restored = Body(new Expand(undos[i]).Visit(root)!);
        }
        if (!TokenEqual(baseline, restored)) throw new Exception("Repetition compression failed its complete expansion proof.");
        ScriptPack.Check(ScriptPack.Compile(source, "repetition output"));
        // Type inference runs before this pass; remaining external type names
        // are evaluated by the subsequent namespace-alias pass.
        foreach (var group in initialRoot.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(n => n.SpanStart >= ScriptPack.Header.Length + boundary)
            .Select(n => initialModel.GetSymbolInfo(n).Symbol as INamedTypeSymbol)
            .Where(s => s != null && !s.Locations.Any(l => l.IsInSource) && s.Name.Length > 12)
            .GroupBy(s => Key(s!)).Where(g => g.Count() > 1))
            report.Add(new("type", group.Key, group.Count(), 0, false, "Retained by the expression pass; evaluated separately by namespace type aliasing."));
        foreach (var kind in report.Where(r => r.Accepted).GroupBy(r => r.Kind))
            Console.WriteLine($"Repetition {kind.Key}: {kind.Count()} accepted cases; {kind.Sum(r => r.Saved):N0} characters saved.");
        return new(source, report);
    }
    static Dictionary<string,Queue<string>> Copy(Dictionary<string,Queue<string>> values) => values.ToDictionary(p => p.Key, p => new Queue<string>(p.Value));
    static bool TokenEqual(string a, string b) => SyntaxFactory.ParseTokens(a, options: ScriptPack.ParseOptions).Select(t => t.Text).SequenceEqual(SyntaxFactory.ParseTokens(b, options: ScriptPack.ParseOptions).Select(t => t.Text));
    static string Body(SyntaxNode root)
    {
        string text = root.ToFullString()[ScriptPack.Header.Length..];
        return text[..text.LastIndexOf('}')];
    }
    static IEnumerable<Candidate> Discover(SyntaxNode root, SemanticModel model, int boundary, HashSet<string> helpers, string alias)
    {
        bool Eligible(SyntaxNode n) => n.SpanStart >= ScriptPack.Header.Length + boundary &&
            !n.Ancestors().OfType<MethodDeclarationSyntax>().Any(m => helpers.Contains(m.Identifier.ValueText)) &&
            !n.Ancestors().OfType<VariableDeclaratorSyntax>().Any(v => helpers.Contains(v.Identifier.ValueText));
        foreach (var group in root.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(n => Eligible(n) && n.IsKind(SyntaxKind.StringLiteralExpression))
            .GroupBy(n => n.Token.ValueText).Where(g => g.Count() > 1))
        {
            string literal = group.OrderBy(n => n.Token.Text.Length).First().Token.Text;
            yield return new("string", group.Key, group.Cast<SyntaxNode>().ToList(), $"const string {alias}={literal};", alias);
        }
        var calls = new Dictionary<string,(IMethodSymbol method,int arity,List<SyntaxNode> nodes)>();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(Eligible))
        {
            if (call.Expression is not MemberAccessExpressionSyntax member || model.GetSymbolInfo(call).Symbol is not IMethodSymbol m ||
                m.Locations.Any(l => l.IsInSource) || m.IsGenericMethod || m.ReducedFrom != null || m.MethodKind != MethodKind.Ordinary ||
                m.DeclaredAccessibility != Accessibility.Public || m.Parameters.Any(p => p.RefKind != RefKind.None || p.IsParams) ||
                m.ReturnsByRef || UnsupportedType(m.ReturnType) || UnsupportedType(m.ContainingType) || m.Parameters.Any(p => UnsupportedType(p.Type)) ||
                call.ArgumentList.Arguments.Any(a => a.NameColon != null || a.RefKindKeyword.RawKind != 0)) continue;
            // Mutable value-type receivers cannot be copied into a helper.
            if (!m.IsStatic && m.ContainingType.IsValueType && !(m.ContainingType.Name == "MyIniValue" && new[]{"ToInt64","ToInt32","ToDouble","ToBoolean","ToString"}.Contains(m.Name))) continue;
            string key = Key(m) + "/" + call.ArgumentList.Arguments.Count;
            if (!calls.TryGetValue(key,out var group)) group=(m,call.ArgumentList.Arguments.Count,new());
            group.nodes.Add(call); calls[key]=group;
        }
        foreach (var pair in calls.Where(p => p.Value.nodes.Count > 1))
        {
            var (m, arity, nodes) = pair.Value;
            var parameters = new List<string>();
            if (!m.IsStatic) parameters.Add(TypeName(m.ContainingType)+" a");
            for (int i=0;i<arity;i++) parameters.Add(TypeName(m.Parameters[i].Type)+" "+(char)('b'+i));
            string receiver = m.IsStatic ? TypeName(m.ContainingType) : "a";
            string args = string.Join(",",Enumerable.Range(0,arity).Select(i=>((char)('b'+i)).ToString()));
            yield return new("method",pair.Key,nodes,$"static {TypeName(m.ReturnType)} {alias}({string.Join(",",parameters)})=>{receiver}.{m.Name}({args});",alias);
        }
        foreach (var group in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Where(Eligible)
            .Select(n=>(node:n,property:model.GetSymbolInfo(n).Symbol as IPropertySymbol))
            .Where(x=>x.property != null && !x.property.Locations.Any(l=>l.IsInSource) && !x.property.IsStatic && !x.property.ContainingType.IsValueType &&
                !x.property.IsIndexer && !x.property.ReturnsByRef && x.property.GetMethod?.DeclaredAccessibility==Accessibility.Public &&
                !UnsupportedType(x.property.Type) && !UnsupportedType(x.property.ContainingType))
            .GroupBy(x=>Key(x.property!)).Where(g=>g.Count()>1))
        {
            var p=group.First().property!;
            if (group.Any(x=>Writable(x.node))) continue;
            yield return new("property",group.Key,group.Select(x=>(SyntaxNode)x.node).ToList(),$"static {TypeName(p.Type)} {alias}({TypeName(p.ContainingType)} a)=>a.{p.Name};",alias);
        }
    }
    sealed class Apply(Candidate candidate, SemanticModel model) : CSharpSyntaxRewriter
    {
        readonly HashSet<(int,int)> positions=candidate.Nodes.Select(n=>(n.SpanStart,n.Span.Length)).ToHashSet();
        internal readonly Dictionary<string,Queue<string>> Originals=new();
        public override SyntaxNode? Visit(SyntaxNode? node)
        {
            if (node==null) return null;
            var rewritten=base.Visit(node)!;
            if (node is not ExpressionSyntax || !positions.Contains((node.SpanStart,node.Span.Length))) return rewritten;
            string replacement;
            if (candidate.Kind=="string") replacement=candidate.Alias;
            else if (rewritten is MemberAccessExpressionSyntax property) replacement=$"{candidate.Alias}({property.Expression})";
            else
            {
                var call=(InvocationExpressionSyntax)rewritten;
                var member=(MemberAccessExpressionSyntax)call.Expression;
                var m=(IMethodSymbol)model.GetSymbolInfo(node).Symbol!;
                var args=call.ArgumentList.Arguments.Select(a=>a.ToString()).ToList();
                if (!m.IsStatic) args.Insert(0,member.Expression.ToString());
                replacement=$"{candidate.Alias}({string.Join(",",args)})";
            }
            var result=SyntaxFactory.ParseExpression(Minify(replacement),options:ScriptPack.ParseOptions).WithTriviaFrom(node);
            string key=Tokens(result);
            if (!Originals.TryGetValue(key,out var values)) Originals[key]=values=new();
            values.Enqueue(node.WithoutTrivia().ToFullString());
            return result;
        }
    }
    sealed class Expand(Undo undo) : CSharpSyntaxRewriter
    {
        readonly Dictionary<string,Queue<string>> originals=Copy(undo.Originals);
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) => node.Identifier.ValueText==undo.Alias ? null : base.VisitMethodDeclaration(node);
        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node) => node.Declaration.Variables.Any(v=>v.Identifier.ValueText==undo.Alias) ? null : base.VisitFieldDeclaration(node);
        public override SyntaxNode? Visit(SyntaxNode? node)
        {
            if (node is ExpressionSyntax && originals.TryGetValue(Tokens(node),out var values) && values.Count>0)
                return SyntaxFactory.ParseExpression(values.Dequeue(),options:ScriptPack.ParseOptions).WithTriviaFrom(node);
            return base.Visit(node);
        }
    }
}
