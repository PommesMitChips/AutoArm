using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Runs before packing so readable PB source also excludes the other PB's role.
internal static class Specializer
{
    internal static string Run(string source)
    {
        while (true)
        {
            var compilation = ScriptPack.Compile(source, "specialization");
            var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length != 0) throw new Exception(string.Join("\n", errors.Select(error => error.ToString())));
            var tree = compilation.SyntaxTrees.Single();
            var root = tree.GetRoot();
            var model = compilation.GetSemanticModel(tree);
            var folded = new Fold(model).Visit(root)!;
            string next = Unwrap(folded);
            if (next != source) { source = next; continue; }

            var references = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(name).Symbol;
                if (symbol != null) references.Add(symbol.OriginalDefinition);
            }
            var remove = new List<SyntaxNode>();
            foreach (var member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                if (member is MethodDeclarationSyntax method)
                {
                    var symbol = model.GetDeclaredSymbol(method)!;
                    // PB entry points, overrides and interface implementations can
                    // be invoked externally rather than through a local reference.
                    if (symbol.ContainingType.Name == "Program" && symbol.Name is "Main" or "Save" ||
                        symbol.IsOverride || symbol.IsVirtual || symbol.IsAbstract ||
                        method.ExplicitInterfaceSpecifier != null || method.AttributeLists.Count != 0 ||
                        ImplementsInterface(symbol)) continue;
                    if (!references.Contains(symbol.OriginalDefinition)) remove.Add(method);
                }
                else if (member is PropertyDeclarationSyntax property)
                {
                    var symbol = model.GetDeclaredSymbol(property)!;
                    if (symbol.IsOverride || symbol.IsVirtual || symbol.IsAbstract ||
                        property.ExplicitInterfaceSpecifier != null || property.AttributeLists.Count != 0 ||
                        ImplementsInterface(symbol) || property.Initializer != null &&
                        !model.GetConstantValue(property.Initializer.Value).HasValue) continue;
                    if (!references.Contains(symbol.OriginalDefinition)) remove.Add(property);
                }
                else if (member is FieldDeclarationSyntax field && field.AttributeLists.Count == 0)
                {
                    var unused = new List<VariableDeclaratorSyntax>();
                    foreach (var variable in field.Declaration.Variables)
                    {
                        var symbol = model.GetDeclaredSymbol(variable)!;
                        if (references.Contains(symbol.OriginalDefinition) || variable.Identifier.Text == "DefaultArm" ||
                            variable.Identifier.Text == "ControllerBuildVersion") continue;
                        // Never discard an initializer with observable effects.
                        if (variable.Initializer != null && !PureInitializer(variable.Initializer.Value, model)) continue;
                        unused.Add(variable);
                    }
                    if (unused.Count == field.Declaration.Variables.Count) remove.Add(field);
                    else remove.AddRange(unused);
                }
            }
            if (remove.Count == 0)
            {
                var lines = tree.GetText().Lines;
                var whitespace = root.DescendantTrivia().Where(trivia => (trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia)) &&
                    lines.GetLineFromPosition(trivia.SpanStart).ToString().Trim().Length == 0);
                // Touch trivia only; whitespace inside literal text is data.
                return Unwrap(root.ReplaceTrivia(whitespace, (original, _) => original.IsKind(SyntaxKind.EndOfLineTrivia)
                    ? SyntaxFactory.EndOfLine("\n") : default));
            }
            source = Unwrap(root.RemoveNodes(remove, SyntaxRemoveOptions.KeepExteriorTrivia)!);
        }
    }

    static bool ImplementsInterface(ISymbol symbol) => symbol.ContainingType.AllInterfaces
        .SelectMany(type => type.GetMembers()).Any(member =>
            SymbolEqualityComparer.Default.Equals(symbol.ContainingType.FindImplementationForInterfaceMember(member), symbol));

    static bool PureInitializer(ExpressionSyntax expression, SemanticModel model)
    {
        if (model.GetConstantValue(expression).HasValue) return true;
        // An unused empty framework collection has no externally observable
        // constructor work. Other constructors/argument expressions stay intact.
        return expression is ObjectCreationExpressionSyntax { Initializer: null } creation &&
            creation.ArgumentList?.Arguments.Count == 0 && model.GetTypeInfo(expression).Type is INamedTypeSymbol type &&
            type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
            type.Name is "Dictionary" or "List" or "HashSet" or "Queue" or "Stack";
    }

    static string Unwrap(SyntaxNode root)
    {
        var program = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        // Preserve comments/spacing and the original scopes rather than reparsing
        // concatenated source fragments or lifting branch-local declarations.
        return string.Concat(program.Members.Select(member => member.ToFullString()));
    }

    sealed class Fold(SemanticModel model) : CSharpSyntaxRewriter
    {
        bool? Value(ExpressionSyntax condition)
        {
            var constant = model.GetConstantValue(condition);
            return constant.HasValue && constant.Value is bool value ? value : null;
        }
        public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
        {
            var value = Value(node.Condition);
            if (!value.HasValue) return base.VisitIfStatement(node);
            var chosen = value.Value ? node.Statement : node.Else?.Statement;
            if (chosen == null) return SyntaxFactory.EmptyStatement().WithTriviaFrom(node);
            var statement = (StatementSyntax)Visit(chosen)!;
            return (statement is BlockSyntax ? statement : SyntaxFactory.Block(statement)).WithTriviaFrom(node);
        }
        public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            var value = Value(node.Condition);
            if (!value.HasValue) return base.VisitConditionalExpression(node);
            var chosen = value.Value ? node.WhenTrue : node.WhenFalse;
            var originalType = model.GetTypeInfo(node).Type;
            var chosenType = model.GetTypeInfo(chosen).Type;
            bool convert = !SymbolEqualityComparer.Default.Equals(originalType, chosenType);
            // A conditional's common type can control overload resolution or
            // numeric promotion. Preserve built-in conversions explicitly; do
            // not rewrite user-defined conversions into an explicit cast.
            if (convert && (originalType == null || model.ClassifyConversion(chosen, originalType).IsUserDefined))
                return base.VisitConditionalExpression(node);
            var expression = (ExpressionSyntax)Visit(chosen)!;
            if (convert) expression = SyntaxFactory.CastExpression(
                SyntaxFactory.ParseTypeName(originalType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                SyntaxFactory.ParenthesizedExpression(expression));
            return Group(expression, node);
        }
        public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (!node.IsKind(SyntaxKind.LogicalAndExpression) && !node.IsKind(SyntaxKind.LogicalOrExpression))
                return base.VisitBinaryExpression(node);
            var left = Value(node.Left);
            if (!left.HasValue) return base.VisitBinaryExpression(node);
            bool skipRight = node.IsKind(SyntaxKind.LogicalAndExpression) ? !left.Value : left.Value;
            // Only fold a constant LEFT operand: evaluating a nonconstant left
            // operand can have effects even when the right side fixes the result.
            return skipRight ? SyntaxFactory.LiteralExpression(left.Value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression).WithTriviaFrom(node) :
                Group((ExpressionSyntax)Visit(node.Right)!, node);
        }
        static ExpressionSyntax Group(ExpressionSyntax expression, SyntaxNode original) =>
            (expression is LiteralExpressionSyntax or IdentifierNameSyntax or InvocationExpressionSyntax or MemberAccessExpressionSyntax or ParenthesizedExpressionSyntax
                ? expression : SyntaxFactory.ParenthesizedExpression(expression)).WithTriviaFrom(original);
        public override SyntaxNode? VisitTryStatement(TryStatementSyntax node)
        {
            var statement = (TryStatementSyntax)base.VisitTryStatement(node)!;
            return statement.Block.Statements.Count == 0 && statement.Finally == null
                ? SyntaxFactory.EmptyStatement().WithTriviaFrom(node) : statement;
        }
        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            var block = (BlockSyntax)base.VisitBlock(node)!;
            return block.WithStatements(SyntaxFactory.List(block.Statements.Where(statement => statement is not EmptyStatementSyntax)));
        }
    }
}
