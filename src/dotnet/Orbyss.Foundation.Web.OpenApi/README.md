# Orbyss.Foundation.Web.OpenApi

An optional `IWebShellFeature` that registers the Orbyss Foundation OpenAPI document and maps it at
`/_orbyss-foundation/openapi/{documentName}.json` within the active shell. Consumers can replace or omit
the feature without changing `Orbyss.Foundation.Host`.
