using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Orbyss.Foundation.Analyzers;

/// <summary>
/// Enforces Orbyss Foundation lifecycle, call-site, source-layout, and documentation policies.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FoundationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Identifies shell-scoped hosted-service registrations.</summary>
    public const string ShellHostedServiceRuleId = "ORB1001";

    /// <summary>Identifies unnamed arguments in complex calls.</summary>
    public const string NamedArgumentsRuleId = "ORB1002";

    /// <summary>Identifies files that declare more than one named type.</summary>
    public const string OneTypePerFileRuleId = "ORB1003";

    /// <summary>Identifies private helper methods that are not placed last.</summary>
    public const string PrivateMethodsLastRuleId = "ORB1004";

    /// <summary>Identifies declared types and members without XML documentation.</summary>
    public const string XmlDocumentationRuleId = "ORB1005";

    /// <summary>Identifies CLR feature names that diverge from package metadata.</summary>
    public const string FeatureIdentityRuleId = "ORB1006";

    /// <summary>Defines the shell-hosted-service diagnostic.</summary>
    private static readonly DiagnosticDescriptor ShellHostedServiceRule = new(
        ShellHostedServiceRuleId,
        "Shell features cannot register hosted services",
        "Register '{0}' through Orbyss.Foundation.Tasks; the Generic Host does not start shell-scoped IHostedService instances",
        "Lifecycle",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CShells creates a provider per shell generation, while the Generic Host starts hosted services only from the root provider.");

    /// <summary>Defines the complex-call named-argument diagnostic.</summary>
    private static readonly DiagnosticDescriptor NamedArgumentsRule = new(
        NamedArgumentsRuleId,
        "Use named arguments for complex calls",
        "Name argument '{0}' because this call supplies four or more arguments",
        "Style",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Orbyss Foundation Policy B asks complex call sites to name arguments while preserving concise obvious calls.");

    /// <summary>Defines the one-declared-type-per-file diagnostic.</summary>
    private static readonly DiagnosticDescriptor OneTypePerFileRule = new(
        OneTypePerFileRuleId,
        "Declare one type per file",
        "Move type '{0}' to its own file; each C# source file may declare only one named type",
        "Structure",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "One named type per file keeps ownership, navigation, and source history explicit.");

    /// <summary>Defines the private-method ordering diagnostic.</summary>
    private static readonly DiagnosticDescriptor PrivateMethodsLastRule = new(
        PrivateMethodsLastRuleId,
        "Put private helper methods last",
        "Move private helper method '{0}' below the type's other members",
        "Structure",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The externally meaningful flow appears before its private implementation helpers.");

    /// <summary>Defines the required XML-documentation diagnostic.</summary>
    private static readonly DiagnosticDescriptor XmlDocumentationRule = new(
        XmlDocumentationRuleId,
        "Document declared types and members",
        "Add purposeful XML documentation to '{0}'",
        "Documentation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every declared type and member has concise contract documentation; design rationale belongs in Architecture or an ADR.");

    /// <summary>Defines the feature-identity convergence diagnostic.</summary>
    private static readonly DiagnosticDescriptor FeatureIdentityRule = new(
        FeatureIdentityRuleId,
        "Declare the governed shell feature identity",
        "Feature type '{0}' must declare [ShellFeature(\"{1}\")] to match FoundationFeatureIdentity",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The CLR feature identity must equal the package descriptor and configured shell identity exactly.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            ShellHostedServiceRule,
            NamedArgumentsRule,
            OneTypePerFileRule,
            PrivateMethodsLastRule,
            XmlDocumentationRule,
            FeatureIdentityRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        context.RegisterSyntaxNodeAction(AnalyzeCompilationUnit, SyntaxKind.CompilationUnit);
        context.RegisterSymbolAction(AnalyzeFeatureIdentity, SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(
            AnalyzePrivateMethodOrder,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration);
        context.RegisterSyntaxNodeAction(
            AnalyzeDocumentation,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.EnumDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.EnumMemberDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DestructorDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.FieldDeclaration,
            SyntaxKind.EventDeclaration,
            SyntaxKind.EventFieldDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration);
    }

    /// <summary>Requires a packaged shell feature to declare the exact MSBuild-governed identity.</summary>
    /// <param name="context">The current symbol-analysis context.</param>
    private static void AnalyzeFeatureIdentity(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } type
            || !ImplementsShellFeature(type)
            || !context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                "build_property.FoundationFeatureIdentity", out var expected)
            || string.IsNullOrWhiteSpace(expected))
        {
            return;
        }

        var attribute = type.GetAttributes().SingleOrDefault(candidate =>
            candidate.AttributeClass?.ToDisplayString() == "CShells.Features.ShellFeatureAttribute");
        var actual = attribute is { ConstructorArguments.Length: > 0 }
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                FeatureIdentityRule,
                type.Locations.FirstOrDefault(),
                type.Name,
                expected));
        }
    }

    /// <summary>Rejects hosted-service registration from a CShells feature and evaluates invocation arguments.</summary>
    /// <param name="context">The invocation operation context.</param>
    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name == "AddHostedService"
            && invocation.TargetMethod.ContainingType.ToDisplayString() ==
            "Microsoft.Extensions.DependencyInjection.ServiceCollectionHostedServiceExtensions"
            && ImplementsShellFeature(context.ContainingSymbol.ContainingType))
        {
            var serviceName = invocation.TargetMethod.TypeArguments.FirstOrDefault()?.Name ?? "hosted service";
            context.ReportDiagnostic(Diagnostic.Create(ShellHostedServiceRule, invocation.Syntax.GetLocation(), serviceName));
        }

        AnalyzeArguments(context, invocation.Arguments);
    }

    /// <summary>Evaluates object-creation arguments against the complex-call policy.</summary>
    /// <param name="context">The object-creation operation context.</param>
    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        AnalyzeArguments(context, creation.Arguments);
    }

    /// <summary>Reports unnamed explicit arguments when a call has four or more arguments.</summary>
    /// <param name="context">The current operation context.</param>
    /// <param name="arguments">The arguments supplied by the operation.</param>
    private static void AnalyzeArguments(
        OperationAnalysisContext context,
        ImmutableArray<IArgumentOperation> arguments)
    {
        if (arguments.Length < 4)
            return;

        foreach (var argument in arguments)
        {
            if (argument.ArgumentKind != ArgumentKind.Explicit
                || argument.Syntax is ArgumentSyntax { NameColon: not null })
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                NamedArgumentsRule,
                argument.Syntax.GetLocation(),
                argument.Parameter?.Name ?? "argument"));
        }
    }

    /// <summary>Reports every named type after the first declaration in a source file.</summary>
    /// <param name="context">The compilation-unit syntax context.</param>
    private static void AnalyzeCompilationUnit(SyntaxNodeAnalysisContext context)
    {
        var compilationUnit = (CompilationUnitSyntax)context.Node;
        var declarations = compilationUnit
            .DescendantNodes()
            .Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            .ToArray();

        foreach (var declaration in declarations.Skip(1))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OneTypePerFileRule,
                declaration.GetLocation(),
                GetDeclarationName(declaration)));
        }
    }

    /// <summary>Reports private methods that have a non-private-helper member below them.</summary>
    /// <param name="context">The containing type syntax context.</param>
    private static void AnalyzePrivateMethodOrder(SyntaxNodeAnalysisContext context)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        for (var index = 0; index < declaration.Members.Count; index++)
        {
            if (declaration.Members[index] is not MethodDeclarationSyntax method
                || !IsPrivateMethod(context, method))
            {
                continue;
            }

            var hasLaterNonPrivateHelper = declaration.Members
                .Skip(index + 1)
                .Any(member => member is not MethodDeclarationSyntax laterMethod
                    || !IsPrivateMethod(context, laterMethod));
            if (hasLaterNonPrivateHelper)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    PrivateMethodsLastRule,
                    method.Identifier.GetLocation(),
                    method.Identifier.ValueText));
            }
        }
    }

    /// <summary>Reports a declaration when it has no leading XML documentation trivia.</summary>
    /// <param name="context">The declaration syntax context.</param>
    private static void AnalyzeDocumentation(SyntaxNodeAnalysisContext context)
    {
        var hasDocumentation = context.Node.GetLeadingTrivia().Any(trivia =>
            trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        if (hasDocumentation)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            XmlDocumentationRule,
            context.Node.GetLocation(),
            GetDeclarationName(context.Node)));
    }

    /// <summary>Determines whether a method's declared accessibility is private.</summary>
    /// <param name="context">The containing syntax analysis context.</param>
    /// <param name="method">The method declaration to inspect.</param>
    /// <returns><see langword="true"/> when the method is private; otherwise, <see langword="false"/>.</returns>
    private static bool IsPrivateMethod(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax method) =>
        context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken)?.DeclaredAccessibility
            == Accessibility.Private;

    /// <summary>Gets a stable diagnostic name for a declared syntax node.</summary>
    /// <param name="declaration">The declaration to name.</param>
    /// <returns>The declared identifier or a declaration-kind fallback.</returns>
    private static string GetDeclarationName(SyntaxNode declaration) =>
        declaration switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
            DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
            EnumMemberDeclarationSyntax enumMember => enumMember.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            DestructorDeclarationSyntax destructor => "~" + destructor.Identifier.ValueText,
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            IndexerDeclarationSyntax => "this[]",
            BaseFieldDeclarationSyntax field => string.Join(", ", field.Declaration.Variables.Select(item => item.Identifier.ValueText)),
            EventDeclarationSyntax @event => @event.Identifier.ValueText,
            OperatorDeclarationSyntax @operator => "operator " + @operator.OperatorToken.ValueText,
            ConversionOperatorDeclarationSyntax conversion => conversion.Type.ToString(),
            _ => declaration.Kind().ToString()
        };

    /// <summary>Determines whether a type implements the CShells feature contract.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type implements the feature contract.</returns>
    private static bool ImplementsShellFeature(INamedTypeSymbol? type) =>
        type is not null && type.AllInterfaces.Any(candidate =>
            candidate.ToDisplayString() == "CShells.Features.IShellFeature");
}
