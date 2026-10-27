using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orbyss.Foundation.Analyzers;

/// <summary>Recognizes native EF migration declarations without excluding authored migration files.</summary>
internal static class EfMigrationLayout
{
    /// <summary>Admits only native relational builder expressions inside the actual Up/Down override.</summary>
    internal static bool IsDeclaration(SyntaxNodeAnalysisContext context, SyntaxNode? node)
    {
        if (node is not InvocationExpressionSyntax invocation || !IsNativeBuilderCall(context, invocation))
            return false;

        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is LocalFunctionStatementSyntax)
                return false;

            if (ancestor is MethodDeclarationSyntax)
                break;
        }

        var method = node
            .Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        if (method is null)
            return false;

        var symbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) as IMethodSymbol;
        var overridden = symbol?.OverriddenMethod;
        if (overridden is null || overridden.Name is not ("Up" or "Down")
            || overridden.ContainingType.ToDisplayString() != "Microsoft.EntityFrameworkCore.Migrations.Migration"
            || overridden.ContainingAssembly.Name != "Microsoft.EntityFrameworkCore.Relational")
        {
            return false;
        }

        return IsDeclarativeSyntax(context, invocation);
    }

    /// <summary>Rejects arbitrary work hidden in builder arguments or deferred constraint bodies.</summary>
    private static bool IsDeclarativeSyntax(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        foreach (var child in node.DescendantNodesAndSelf())
        {
            if (child is InvocationExpressionSyntax call && !IsNativeBuilderCall(context, call))
                return false;

            if (child is BaseObjectCreationExpressionSyntax
                or AwaitExpressionSyntax
                or AssignmentExpressionSyntax
                or ThrowExpressionSyntax
                or ThrowStatementSyntax
                or LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Uses exact EF relational symbols, never filenames, namespaces alone or package prefixes.</summary>
    private static bool IsNativeBuilderCall(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax call)
    {
        var info = context.SemanticModel.GetSymbolInfo(call, context.CancellationToken);
        if (info.Symbol is not IMethodSymbol method)
            return false;

        var type = method.ContainingType.OriginalDefinition;
        if (type.ContainingAssembly.Name != "Microsoft.EntityFrameworkCore.Relational")
            return false;

        var name = type.ToDisplayString();
        return name is "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder"
            or "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.ColumnsBuilder"
            or "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder<TColumns>"
            or "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.OperationBuilder<TOperation>"
            or "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.AlterOperationBuilder<TOperation>";
    }
}
