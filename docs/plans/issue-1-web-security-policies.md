# Issue 1: validated shell web security policies

Issue: https://github.com/orbyss-io/dotnet-foundation/issues/1

Status: implemented locally as the first issue in the shared 0.2.0 bump. See [validation evidence](../validation-0.2.0.md). Consumer migration and publication have not been performed.

## Outcome

A consumer selects complete, validated response policies through application/shell/feature settings and endpoint metadata. Foundation owns binding, validation, selection, and final response-header emission. Resource authorization and same-origin request admission remain independent.

## Agreed decisions

- Compile immutable policy catalogs for a shell lifetime. Changes require shell recreation; no live reload in this delivery.
- Select a complete named policy rather than merging header fields.
- Apply application configuration, then shell overrides. Policy selection precedence is endpoint, feature, shell default, Foundation default. Ambiguous feature selection fails validation.
- Preserve mandatory private-response `no-store` and current framing restrictions. Embedding remains outside this delivery.
- Immutable public-asset caching applies only to successful admitted assets and valid conditional responses. Errors and rejected asset requests use `no-store`.
- A public asset is explicitly public; anonymity, GET, extension, or authentication state does not establish cache eligibility.

## Current implementation

`FoundationWebDefaultsFeature` binds `Foundation:Web`, but its options currently cover only localization. `CorrelationAndSecurityHeadersMiddleware` writes fixed headers before downstream middleware. Problem Details runs later and may clear an error response. Discovery separately writes `nosniff` and indexing headers. No Foundation source currently sets `Cache-Control`.

The existing Host registers CShells and activates configured shells. It loads its JSON configuration without live reload. Preserve its application-neutral role and existing activation failure policy.

## Implementation sequence

1. Add a focused real-HTTP probe to establish endpoint metadata availability, middleware ordering, response clearing, and static response behavior with pinned CShells. Use these results to place policy resolution and final emission correctly.
2. Define binding options, named document/public-asset/private-response policies, endpoint/feature selection contracts, and immutable compiled catalogs in the existing WebDefaults package. Explicitly validate CSP, framing, referrer, Permissions-Policy, caching, and indexing interactions; reject unsafe header values and contradictory declarations.
3. Validate the catalog and configured policy references during shell activation. Report the shell, configuration path, policy name, and conflict without exposing sensitive configuration. Validate endpoint selections before admitting traffic wherever endpoint construction permits.
4. Separate correlation handling from policy emission. Register one early response-start callback and capture the resolved immutable policy per request. Define conservative behavior for requests without endpoint metadata, response errors, and static middleware that bypasses endpoints.
5. Establish ownership when WebDefaults is selected. Adapt Discovery and built-in sensitive endpoints to explicit metadata; preserve Discovery's safe standalone behavior through a documented optional integration. Sensitive anonymous BFF responses must still receive private/no-store treatment.
6. Add configuration-only consumption documentation, a small Foundation-owned example, the dedicated HTTP probe, and its validator. Wire validation into the repository's established checks.

## Regression evidence

- Two concurrent shells with different policies and reused policy names; no shared mutable state or leaked defaults.
- Application/shell/feature/endpoint precedence; unknown policies; ambiguous feature attribution; invalid and contradictory headers; denied weakening of mandatory restrictions.
- Success, GET/HEAD, redirects, 404/405, exceptions, response clearing, 401/403, antiforgery and DPoP early rejection, and Discovery/static responses.
- One final value for each owned header despite downstream conflicts; independent authorization remains effective.
- Private and sensitive anonymous responses remain `no-store`; only admitted successful immutable assets receive public caching; invalid conditional/error responses cannot inherit it.
- Shell recreation applies new settings; existing requests retain their captured policy. Invalid activation follows the existing Host activation policy without silently substituting permissive defaults.

## Completion and limits

Run locked restore, Release build, focused and existing validators, and package validation. Document response coverage inside a selected shell; proxy responses and failures before shell selection cannot promise that shell's policy. Deliver adoption instructions and regression results without changing PriceCalculator or Program Kit, tagging, or publishing.

## Implementation verification

Public API naming and exact feature-to-endpoint association must be supported by the CShells probe. Keep this capability in the existing WebDefaults package. Any framework limitation must be made explicit during implementation review rather than weakening the requested feature-level selection silently.
