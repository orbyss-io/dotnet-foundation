namespace Orbyss.Foundation.OpenApiExport;

/// <summary>Defines the platform feature identities supplied by Orbyss Foundation runtime packages.</summary>
internal static class BuiltInFeatures
{
    /// <summary>Maps exact feature identities to their package and composition metadata.</summary>
    public static readonly IReadOnlyDictionary<string, BuiltInFeatureDefinition> Definitions =
        new Dictionary<string, BuiltInFeatureDefinition>(StringComparer.Ordinal)
        {
            ["Orbyss.Foundation.Authentication"] =
                new("Orbyss.Foundation.Authentication", [], [], false),
            ["Orbyss.Foundation.Authentication.BffCookie"] =
                new(
                    "Orbyss.Foundation.Authentication.BffCookie",
                    ["Orbyss.Foundation.Authentication", "Orbyss.Foundation.WebDefaults"],
                    ["/bff/login", "/bff/user", "/bff/antiforgery", "/bff/logout", "/bff/signed-out"],
                    false),
            ["Orbyss.Foundation.Authentication.SpaPkce"] =
                new(
                    "Orbyss.Foundation.Authentication.SpaPkce",
                    ["Orbyss.Foundation.Authentication", "Orbyss.Foundation.WebDefaults"],
                    [],
                    false),
            ["Orbyss.Foundation.DomainEvents"] =
                new("Orbyss.Foundation.DomainEvents", [], [], false),
            ["Orbyss.Foundation.Localization.Web.Management"] =
                new(
                    "Orbyss.Foundation.Localization.Web.Management",
                    [],
                    [
                        "/_orbyss-foundation/localization/catalogs",
                        "/_orbyss-foundation/localization/catalogs/{catalogId}",
                        "/_orbyss-foundation/localization/catalogs/validate",
                        "/_orbyss-foundation/localization/catalogs/{catalogId}/imports/preview",
                        "/_orbyss-foundation/localization/catalogs/{catalogId}/imports/{previewId}/apply",
                        "/_orbyss-foundation/localization/catalogs/{catalogId}/exports",
                        "/_orbyss-foundation/localization/releases/{releaseId}",
                        "/_orbyss-foundation/localization/releases/{baselineId}/diff/{candidateId}",
                        "/_orbyss-foundation/localization/catalogs/{catalogId}/{revision:long}/review",
                        "/_orbyss-foundation/localization/catalogs/{catalogId}/{revision:long}/approve",
                        "/_orbyss-foundation/localization/catalogs/{catalogId}/{revision:long}/publish",
                        "/_orbyss-foundation/localization/releases/{releaseId}/retire",
                    ],
                    false),
            ["Orbyss.Foundation.Localization.Web.Runtime"] =
                new(
                    "Orbyss.Foundation.Localization.Web.Runtime",
                    [],
                    [
                        "/_orbyss-foundation/localization/runtime/bundles/{scopeKind}/{languageTag}",
                        "/_orbyss-foundation/localization/runtime/messages/{scopeKind}/{languageTag}/{key}",
                    ],
                    false),
            ["FoundationTasks"] =
                new("Orbyss.Foundation.Tasks", [], [], false),
            ["Orbyss.Foundation.WebDefaults"] =
                new("Orbyss.Foundation.WebDefaults", [], [], false),
            ["Orbyss.Foundation.Web.OpenApi"] =
                new("Orbyss.Foundation.Web.OpenApi", [], ["/_orbyss-foundation/openapi/{documentName}.json"], true),
            ["Orbyss.Foundation.Web.Discovery"] =
                new("Orbyss.Foundation.Web.Discovery", [], ["/robots.txt", "/sitemap.xml"], false),
            ["Orbyss.Foundation.Web.ProblemDetails"] =
                new("Orbyss.Foundation.Web.ProblemDetails", [], [], false),
        };
}
