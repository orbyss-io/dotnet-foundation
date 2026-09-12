# Orbyss.Foundation.Json.AspNetCore

Select the `Orbyss.Foundation.Json.AspNetCore` shell feature. It registers a shell-local JsonProfileCatalog
and maps JsonProfileException to Problem Details with safe `code` and `traceId` fields (413 for byte limits;
400 for malformed/invalid input). It creates no public endpoints and does not modify global HttpJsonOptions.

Configure the shell's `Configuration:Foundation:Json:Profiles`:

```json
{
  "strict-request": { "Preset": "strict-request", "MaxBytes": 1048576, "MaxDepth": 32 },
  "provider-response": { "Preset": "tolerant-response", "MaxBytes": 262144, "Extensions": [] }
}
```

Endpoints deliberately inject JsonProfileCatalog and call
`profiles.Get("strict-request").ReadAsync<MyRequest>(context.Request.Body, context.RequestAborted)`.
There is no implicit endpoint profile inferred from DTO type. Code registers IJsonProfileExtension through
DI; settings select allowlisted IDs only. Changes require shell recreation. Strict guarantees cannot be
weakened through settings. Response security and authorization remain separate features.
