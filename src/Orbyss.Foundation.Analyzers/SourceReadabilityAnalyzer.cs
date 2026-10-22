using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Orbyss.Foundation.Analyzers;

/// <summary>Enforces the explicitly selected source-readability contract on authored C#.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceReadabilityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Identifies parameter and argument lists that need vertical layout.</summary>
    public const string ListLayoutRuleId = "ORB1007";

    /// <summary>Identifies argument values or deferred bodies that must be prepared separately.</summary>
    public const string PreparedArgumentsRuleId = "ORB1008";

    /// <summary>Identifies fluent chains that combine their receiver or calls on a line.</summary>
    public const string InvocationChainRuleId = "ORB1009";

    /// <summary>Identifies fields that follow other type members.</summary>
    public const string FieldsFirstRuleId = "ORB1010";

    /// <summary>Keeps the explicitly selected mandatory contract from being downgraded or suppressed.</summary>
    private static readonly string[] MandatoryRuleTags = [WellKnownDiagnosticTags.NotConfigurable];

    /// <summary>Defines the vertical list-layout diagnostic.</summary>
    private static readonly DiagnosticDescriptor ListLayoutRule = new(
        ListLayoutRuleId,
        "Lay out parameter and argument lists vertically",
        "Put each {0} after the opening parenthesis on a separate line and align the closing parenthesis with the opening line's indentation",
        "Style",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: MandatoryRuleTags
    );

    /// <summary>Defines the prepared-argument diagnostic.</summary>
    private static readonly DiagnosticDescriptor PreparedArgumentsRule = new(
        PreparedArgumentsRuleId,
        "Prepare argument values and delegates before the call",
        "Prepare this argument's invocation, constructed value, or deferred body separately, preserving evaluation order and lifetime",
        "Style",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: MandatoryRuleTags
    );

    /// <summary>Defines the invocation-chain diagnostic.</summary>
    private static readonly DiagnosticDescriptor InvocationChainRule = new(
        InvocationChainRuleId,
        "Put the receiver and each chained invocation on separate lines",
        "Put the receiver on its own line and every chained invocation on a following indented line",
        "Style",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: MandatoryRuleTags
    );

    /// <summary>Defines the fields-first diagnostic.</summary>
    private static readonly DiagnosticDescriptor FieldsFirstRule = new(
        FieldsFirstRuleId,
        "Put fields before other type members",
        "Move this field before the type's constructors and other members",
        "Structure",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: MandatoryRuleTags
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            ListLayoutRule,
            PreparedArgumentsRule,
            InvocationChainRule,
            FieldsFirstRule
        );

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(ConfigureCompilation);
    }

    /// <summary>Reads the one compiler-visible source profile shared by native policy producers.</summary>
    /// <param name="options">The compilation's actual native analyzer options.</param>
    /// <returns>Whether the source-readability profile is explicitly selected.</returns>
    internal static bool IsSelected(AnalyzerOptions options) => IsSelected(options.AnalyzerConfigOptionsProvider);

    /// <summary>Reads the same native selection from analyzer and generator configuration.</summary>
    internal static bool IsSelected(AnalyzerConfigOptionsProvider provider) =>
        provider.GlobalOptions.TryGetValue(
            "build_property.FoundationSourceReadability",
            out var enabled
        ) && string.Equals(
            enabled,
            "true",
            StringComparison.OrdinalIgnoreCase
        );

    /// <summary>Registers only the explicitly selected contract, without changing other publisher profiles.</summary>
    private static void ConfigureCompilation(CompilationStartAnalysisContext start)
    {
        if (!IsSelected(start.Options))
        {
            return;
        }

        start.RegisterSyntaxNodeAction(AnalyzeParameterList, SyntaxKind.ParameterList);
        start.RegisterSyntaxNodeAction(AnalyzeArgumentList, SyntaxKind.ArgumentList);
        start.RegisterSyntaxNodeAction(
            AnalyzeChain,
            SyntaxKind.InvocationExpression,
            SyntaxKind.ConditionalAccessExpression
        );
        start.RegisterSyntaxNodeAction(
            AnalyzeFields,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.InterfaceDeclaration
        );
    }

    /// <summary>Checks method, constructor, primary-constructor, delegate and lambda parameter lists.</summary>
    private static void AnalyzeParameterList(SyntaxNodeAnalysisContext context)
    {
        var list = (ParameterListSyntax)context.Node;
        AnalyzeLayout(
            context,
            list.OpenParenToken,
            list.CloseParenToken,
            list.Parameters,
            "parameter"
        );
    }

    /// <summary>Checks call layout and reports at most one prepared-value finding per argument.</summary>
    private static void AnalyzeArgumentList(SyntaxNodeAnalysisContext context)
    {
        var list = (ArgumentListSyntax)context.Node;
        if (IsNameOf(context, list.Parent))
            return;

        AnalyzeLayout(
            context,
            list.OpenParenToken,
            list.CloseParenToken,
            list.Arguments,
            "argument"
        );
        foreach (var argument in list.Arguments)
        {
            if (HasUnpreparedValue(context, argument.Expression))
            {
                var location = argument.GetLocation();
                var diagnostic = Diagnostic.Create(PreparedArgumentsRule, location);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>Checks nonempty lists with more than two items or an existing line break.</summary>
    private static void AnalyzeLayout<T>(
        SyntaxNodeAnalysisContext context,
        SyntaxToken open,
        SyntaxToken close,
        SeparatedSyntaxList<T> items,
        string itemName
    ) where T : SyntaxNode
    {
        if (items.Count == 0 || open.IsMissing || close.IsMissing)
            return;

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var openLine = text.Lines.GetLineFromPosition(open.SpanStart);
        var closeLine = text.Lines.GetLineFromPosition(close.SpanStart);
        if (items.Count <= 2 && openLine.LineNumber == closeLine.LineNumber)
            return;

        var previousLine = openLine.LineNumber;
        var valid = true;
        foreach (var item in items)
        {
            var firstLine = text.Lines.GetLineFromPosition(item.SpanStart).LineNumber;
            valid &= firstLine > previousLine;
            previousLine = text.Lines.GetLineFromPosition(item.Span.End - 1).LineNumber;
        }

        valid &= closeLine.LineNumber > previousLine;
        var closingPrefix = TextSpan.FromBounds(closeLine.Start, close.SpanStart);
        valid &= text.ToString(closingPrefix) == GetIndentation(openLine);
        if (!valid)
        {
            var location = context.Node.GetLocation();
            var diagnostic = Diagnostic.Create(
                ListLayoutRule,
                location,
                itemName
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>Finds runtime calls, allocations and deferred bodies while excluding compile-time nameof operands.</summary>
    private static bool HasUnpreparedValue(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        if (IsNameOf(context, node))
            return false;

        if (node is InvocationExpressionSyntax
            or BaseObjectCreationExpressionSyntax
            or AnonymousObjectCreationExpressionSyntax
            or ArrayCreationExpressionSyntax
            or ImplicitArrayCreationExpressionSyntax
            or StackAllocArrayCreationExpressionSyntax
            or ImplicitStackAllocArrayCreationExpressionSyntax
            or WithExpressionSyntax
            or CollectionExpressionSyntax
            or AnonymousFunctionExpressionSyntax)
        {
            return true;
        }

        foreach (var child in node.ChildNodes())
        {
            if (HasUnpreparedValue(context, child))
                return true;
        }

        return false;
    }

    /// <summary>Recognizes the language operator rather than a method merely named nameof.</summary>
    private static bool IsNameOf(SyntaxNodeAnalysisContext context, SyntaxNode? node) =>
        node is InvocationExpressionSyntax
        && context.SemanticModel.GetOperation(node, context.CancellationToken)
            is Microsoft.CodeAnalysis.Operations.INameOfOperation;

    /// <summary>Checks the receiver spine, including parentheses, intervening properties and conditional access.</summary>
    private static void AnalyzeChain(SyntaxNodeAnalysisContext context)
    {
        var expression = (ExpressionSyntax)context.Node;
        var top = (SyntaxNode)expression;
        while (top.Parent is ParenthesizedExpressionSyntax
            || top.Parent is MemberAccessExpressionSyntax member && member.Expression == top
            || top.Parent is ConditionalAccessExpressionSyntax)
        {
            top = top.Parent;
        }

        if (top.Parent is InvocationExpressionSyntax)
            return;

        var owner = (ExpressionSyntax)top;
        while (owner is ParenthesizedExpressionSyntax parentheses)
            owner = parentheses.Expression;

        if (owner is ConditionalAccessExpressionSyntax && owner != expression)
            return;

        expression = (ExpressionSyntax)top;
        var calls = new List<SyntaxToken>();
        var receiver = CollectChain(expression, calls);
        if (calls.Count < 2)
            return;

        var text = expression.SyntaxTree.GetText(context.CancellationToken);
        var previousLine = text.Lines.GetLineFromPosition(receiver.Span.End - 1).LineNumber;
        var receiverLine = text.Lines.GetLineFromPosition(receiver.SpanStart);
        var receiverIndent = GetIndentation(receiverLine).Length;
        foreach (var call in calls)
        {
            var line = text.Lines.GetLineFromPosition(call.SpanStart);
            var prefix = TextSpan.FromBounds(line.Start, call.SpanStart);
            var indentation = GetIndentation(line);
            if (line.LineNumber <= previousLine
                || indentation.Length <= receiverIndent
                || text.ToString(prefix) != indentation)
            {
                var location = expression.GetLocation();
                var diagnostic = Diagnostic.Create(InvocationChainRule, location);
                context.ReportDiagnostic(diagnostic);
                return;
            }

            previousLine = line.LineNumber;
        }
    }

    /// <summary>Collects chained call names in execution order without walking arguments or deferred bodies.</summary>
    private static ExpressionSyntax CollectChain(ExpressionSyntax expression, List<SyntaxToken> calls)
    {
        if (expression is ParenthesizedExpressionSyntax parentheses)
        {
            var count = calls.Count;
            var inner = CollectChain(parentheses.Expression, calls);
            return count == calls.Count ? parentheses : inner;
        }

        if (expression is ConditionalAccessExpressionSyntax conditional)
        {
            var receiver = CollectChain(conditional.Expression, calls);
            var conditionalStart = calls.Count;
            CollectChain(conditional.WhenNotNull, calls);
            var bindingStart = conditional.WhenNotNull.GetFirstToken();
            if (calls.Count > conditionalStart && calls[conditionalStart] == bindingStart)
                calls[conditionalStart] = conditional.OperatorToken;

            return receiver;
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax member)
            {
                var receiver = CollectChain(member.Expression, calls);
                calls.Add(member.OperatorToken);
                return receiver;
            }

            if (invocation.Expression is MemberBindingExpressionSyntax binding)
            {
                calls.Add(binding.OperatorToken);
                return binding;
            }

            var firstToken = invocation.Expression.GetFirstToken();
            calls.Add(firstToken);
            return invocation.Expression;
        }

        if (expression is MemberAccessExpressionSyntax property)
            return CollectChain(property.Expression, calls);

        return expression;
    }

    /// <summary>Requires declared fields, including constants and field-like events, to precede other members.</summary>
    private static void AnalyzeFields(SyntaxNodeAnalysisContext context)
    {
        var type = (TypeDeclarationSyntax)context.Node;
        var sawOther = false;
        foreach (var member in type.Members)
        {
            if (member is BaseFieldDeclarationSyntax)
            {
                if (sawOther)
                {
                    var location = member.GetLocation();
                    var diagnostic = Diagnostic.Create(FieldsFirstRule, location);
                    context.ReportDiagnostic(diagnostic);
                }
            }
            else
            {
                sawOther = true;
            }
        }
    }

    /// <summary>Returns the actual leading whitespace, retaining tabs for exact parenthesis alignment.</summary>
    private static string GetIndentation(TextLine line)
    {
        var text = line.ToString();
        var whitespace = text
            .TakeWhile(char.IsWhiteSpace)
            .ToArray();
        return new string(whitespace);
    }
}
