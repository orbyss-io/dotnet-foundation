# Orbyss.Foundation.OpenApi.Exporter

Orbyss Foundation-managed build tool that composes OpenAPI from a consumer application's validated,
staged feature-package closure without opening a network listener or running shell lifecycle
initializers. Consumer repositories invoke it through `.orbyss-foundation/eng/openapi_pipeline.py`; do not
add this package to feature or application projects.
