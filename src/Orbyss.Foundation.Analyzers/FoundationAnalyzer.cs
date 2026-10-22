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
        description: "CShells creates a provider per shell generation, while the Generic Host starts hosted services only from the root provider."
    );

    /// <summary>Defines the complex-call named-argument diagnostic.</summary>
    private static readonly DiagnosticDescriptor NamedArgumentsRule = new(
        NamedArgumentsRuleId,
        "Use named arguments for complex calls",
        "Name argument '{0}' because this call supplies four or more arguments",
        "Style",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Orbyss Foundation Policy B asks complex call sites to name arguments while preserving concise obvious calls."
    );

    /// <summary>Defines the one-declared-type-per-file diagnostic.</summary>
    private static readonly DiagnosticDescriptor OneTypePerFileRule = new(
        OneTypePerFileRuleId,
        "Declare one type per file",
        "Move type '{0}' to its own file; each C# source file may declare only one named type",
        "Structure",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "One named type per file keeps ownership, navigation, and source history explicit."
    );

    /// <summary>Defines the private-method ordering diagnostic.</summary>
    private static readonly DiagnosticDescriptor PrivateMethodsLastRule = new(
        PrivateMethodsLastRuleId,
        "Put private helper methods last",
        "Move private helper method '{0}' below the type's other members",
        "Structure",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The externally meaningful flow appears before its private implementation helpers."
    );

    /// <summary>Defines the required XML-documentation diagnostic.</summary>
    private static readonly DiagnosticDescriptor XmlDocumentationRule = new(
        XmlDocumentationRuleId,
        "Document declared types and members",
        "Add purposeful XML documentation to '{0}'",
        "Documentation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every declared type and member has concise contract documentation; design rationale belongs in Architecture or an ADR."
    );

    /// <summary>Defines the feature-identity convergence diagnostic.</summary>
    private static readonly DiagnosticDescriptor FeatureIdentityRule = new(
        FeatureIdentityRuleId,
        "Declare the governed shell feature identity",
        "Feature type '{0}' must declare [ShellFeature(\"{1}\")] to match FoundationFeatureIdentity",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The CLR feature identity must equal the package descriptor and configured shell identity exactly."
    );

    /// <summary>Retains the existing configurable diagnostics for unselected consumers.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> LegacyRules = ImmutableArray.Create(
        ShellHostedServiceRule,
        NamedArgumentsRule,
        OneTypePerFileRule,
        PrivateMethodsLastRule,
        XmlDocumentationRule,
        FeatureIdentityRule
    );

    /// <summary>Protects errors only in the explicitly selected source profile.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SelectedRules = LegacyRules
        .Select(ProtectMandatory)
        .ToImmutableArray();

    /// <summary>Indexes immutable selected descriptors without shared mutable compilation state.</summary>
    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> SelectedRuleById = SelectedRules.ToImmutableDictionary(DiagnosticIdentity);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => LegacyRules;

    /// <summary>Exposes the same existing policies through the selected native analyzer.</summary>
    internal static ImmutableArray<DiagnosticDescriptor> SelectedDiagnostics => SelectedRules;

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(ConfigureLegacyCompilation);
    }

    /// <summary>Registers one mutually exclusive producer for the existing policy checks.</summary>
    /// <param name="context">The compilation whose immutable profile choice controls descriptor selection.</param>
    /// <param name="selected">Whether the explicitly selected profile protects mandatory errors.</param>
    internal static void RegisterRules(
        CompilationStartAnalysisContext context,
        bool selected
    )
    {
        Action<OperationAnalysisContext> invocation = operation => AnalyzeInvocation(operation, selected);
        Action<SymbolAnalysisContext> identity = symbol => AnalyzeFeatureIdentity(symbol, selected);
        Action<SyntaxNodeAnalysisContext> compilationUnit = syntax => AnalyzeCompilationUnit(syntax, selected);
        Action<SyntaxNodeAnalysisContext> privateOrder = syntax => AnalyzePrivateMethodOrder(syntax, selected);
        Action<SyntaxNodeAnalysisContext> documentation = syntax => AnalyzeDocumentation(syntax, selected);
        context.RegisterOperationAction(invocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        context.RegisterSyntaxNodeAction(compilationUnit, SyntaxKind.CompilationUnit);
        context.RegisterSymbolAction(identity, SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(
            privateOrder,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration
        );
        context.RegisterSyntaxNodeAction(
            documentation,
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
            SyntaxKind.ConversionOperatorDeclaration
        );
    }

    /// <summary>Preserves legacy configurability only when the source profile is unselected.</summary>
    private static void ConfigureLegacyCompilation(CompilationStartAnalysisContext context)
    {
        if (!SourceReadabilityAnalyzer.IsSelected(context.Options))
            RegisterRules(context, selected: false);
    }

    /// <summary>Requires a packaged shell feature to declare the exact MSBuild-governed identity.</summary>
    /// <param name="context">The current symbol-analysis context.</param>
    /// <param name="selected">Whether the source profile protects this mandatory error.</param>
    private static void AnalyzeFeatureIdentity(
        SymbolAnalysisContext context,
        bool selected
    )
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } type
            || !ImplementsShellFeature(type)
            || !context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                "build_property.FoundationFeatureIdentity",
                out var expected
            )
            || string.IsNullOrWhiteSpace(expected))
        {
            return;
        }

        var attributes = type.GetAttributes();
        Func<AttributeData, bool> isShellFeature = candidate =>
            candidate.AttributeClass?.ToDisplayString() == "CShells.Features.ShellFeatureAttribute";
        var attribute = attributes.SingleOrDefault(isShellFeature);
        var actual = attribute is { ConstructorArguments.Length: > 0 }
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        if (!string.Equals(
            actual,
            expected,
            StringComparison.Ordinal
        ))
        {
            var rule = SelectRule(FeatureIdentityRule, selected);
            var location = type.Locations.FirstOrDefault();
            var diagnostic = Diagnostic.Create(
                rule,
                location,
                type.Name,
                expected
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>Rejects hosted-service registration from a CShells feature and evaluates invocation arguments.</summary>
    /// <param name="context">The invocation operation context.</param>
    /// <param name="selected">Whether the source profile protects this mandatory error.</param>
    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        bool selected
    )
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name == "AddHostedService"
            && invocation.TargetMethod.ContainingType.ToDisplayString() ==
            "Microsoft.Extensions.DependencyInjection.ServiceCollectionHostedServiceExtensions"
            && ImplementsShellFeature(context.ContainingSymbol.ContainingType))
        {
            var serviceName = invocation.TargetMethod.TypeArguments.FirstOrDefault()?.Name ?? "hosted service";
            var rule = SelectRule(ShellHostedServiceRule, selected);
            var location = invocation.Syntax.GetLocation();
            var diagnostic = Diagnostic.Create(
                rule,
                location,
                serviceName
            );
            context.ReportDiagnostic(diagnostic);
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
        ImmutableArray<IArgumentOperation> arguments
    )
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

            var location = argument.Syntax.GetLocation();
            var parameterName = argument.Parameter?.Name ?? "argument";
            var diagnostic = Diagnostic.Create(
                NamedArgumentsRule,
                location,
                parameterName
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>Reports every named type after the first declaration in a source file.</summary>
    /// <param name="context">The compilation-unit syntax context.</param>
    /// <param name="selected">Whether the source profile protects this mandatory error.</param>
    private static void AnalyzeCompilationUnit(
        SyntaxNodeAnalysisContext context,
        bool selected
    )
    {
        var compilationUnit = (CompilationUnitSyntax)context.Node;
        var descendants = compilationUnit.DescendantNodes();
        Func<SyntaxNode, bool> isNamedType = node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;
        var declarations = descendants
            .Where(isNamedType)
            .ToArray();

        foreach (var declaration in declarations.Skip(1))
        {
            var rule = SelectRule(OneTypePerFileRule, selected);
            var location = declaration.GetLocation();
            var name = GetDeclarationName(declaration);
            var diagnostic = Diagnostic.Create(
                rule,
                location,
                name
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>Reports private methods that have a non-private-helper member below them.</summary>
    /// <param name="context">The containing type syntax context.</param>
    /// <param name="selected">Whether the source profile protects this mandatory error.</param>
    private static void AnalyzePrivateMethodOrder(
        SyntaxNodeAnalysisContext context,
        bool selected
    )
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        for (var index = 0; index < declaration.Members.Count; index++)
        {
            if (declaration.Members[index] is not MethodDeclarationSyntax method
                || !IsPrivateMethod(context, method))
            {
                continue;
            }

            var laterMembers = declaration.Members.Skip(index + 1);
            Func<MemberDeclarationSyntax, bool> isNonPrivate = member =>
                member is not MethodDeclarationSyntax laterMethod || !IsPrivateMethod(context, laterMethod);
            var hasLaterNonPrivateHelper = laterMembers.Any(isNonPrivate);
            if (hasLaterNonPrivateHelper)
            {
                var rule = SelectRule(PrivateMethodsLastRule, selected);
                var location = method.Identifier.GetLocation();
                var diagnostic = Diagnostic.Create(
                    rule,
                    location,
                    method.Identifier.ValueText
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>Reports a declaration when it has no leading XML documentation trivia.</summary>
    /// <param name="context">The declaration syntax context.</param>
    /// <param name="selected">Whether the source profile protects this mandatory error.</param>
    private static void AnalyzeDocumentation(
        SyntaxNodeAnalysisContext context,
        bool selected
    )
    {
        var leadingTrivia = context.Node.GetLeadingTrivia();
        Func<SyntaxTrivia, bool> isDocumentation = trivia =>
            trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
        var hasDocumentation = leadingTrivia.Any(isDocumentation);
        if (hasDocumentation)
            return;

        var rule = SelectRule(XmlDocumentationRule, selected);
        var location = context.Node.GetLocation();
        var name = GetDeclarationName(context.Node);
        var diagnostic = Diagnostic.Create(
            rule,
            location,
            name
        );
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>Determines whether a method's declared accessibility is private.</summary>
    /// <param name="context">The containing syntax analysis context.</param>
    /// <param name="method">The method declaration to inspect.</param>
    /// <returns><see langword="true"/> when the method is private; otherwise, <see langword="false"/>.</returns>
    private static bool IsPrivateMethod(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax method
    ) =>
        context.SemanticModel.GetDeclaredSymbol(
            method,
            context.CancellationToken
        )?.DeclaredAccessibility
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
            BaseFieldDeclarationSyntax field => GetFieldNames(field),
            EventDeclarationSyntax @event => @event.Identifier.ValueText,
            OperatorDeclarationSyntax @operator => "operator " + @operator.OperatorToken.ValueText,
            ConversionOperatorDeclarationSyntax conversion => conversion.Type.ToString(),
            _ => GetDeclarationKind(declaration)
        };

    /// <summary>Determines whether a type implements the CShells feature contract.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type implements the feature contract.</returns>
    private static bool ImplementsShellFeature(INamedTypeSymbol? type) =>
        type is not null && type.AllInterfaces.Any(IsShellFeatureInterface);

    /// <summary>Formats declared field names without burying deferred work inside a call argument.</summary>
    private static string GetFieldNames(BaseFieldDeclarationSyntax field)
    {
        Func<VariableDeclaratorSyntax, string> name = item => item.Identifier.ValueText;
        var names = field.Declaration.Variables.Select(name);
        return string.Join(", ", names);
    }

    /// <summary>Matches the existing provider feature contract by its fully qualified identity.</summary>
    private static bool IsShellFeatureInterface(INamedTypeSymbol candidate) =>
        candidate.ToDisplayString() == "CShells.Features.IShellFeature";

    /// <summary>Formats the existing fallback syntax kind through an explicit prepared value.</summary>
    private static string GetDeclarationKind(SyntaxNode declaration)
    {
        var kind = declaration.Kind();
        return kind.ToString();
    }
    /// <summary>Selects immutable profile descriptors while reusing the same source policy.</summary>
    private static DiagnosticDescriptor SelectRule(DiagnosticDescriptor rule, bool selected) =>
        selected ? SelectedRuleById[rule.Id] : rule;

    /// <summary>Retains recommendations while protecting selected mandatory errors.</summary>
    private static DiagnosticDescriptor ProtectMandatory(DiagnosticDescriptor rule)
    {
        if (rule.DefaultSeverity != DiagnosticSeverity.Error)
            return rule;

        return new DiagnosticDescriptor(
            rule.Id,
            rule.Title,
            rule.MessageFormat,
            rule.Category,
            rule.DefaultSeverity,
            rule.IsEnabledByDefault,
            rule.Description,
            rule.HelpLinkUri,
            WellKnownDiagnosticTags.NotConfigurable
        );
    }

    /// <summary>Indexes descriptors by their stable existing diagnostic identity.</summary>
    private static string DiagnosticIdentity(DiagnosticDescriptor rule) => rule.Id;

}
