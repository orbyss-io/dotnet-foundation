# Changelog

## 0.2.2

- Register shell-owned OIDC sign-in, signed-out and remote-sign-out callback routes in the BFF feature, preserving handler validation and private response policies.
- Keep callback failure redirects inside prefixed shells and reject ambiguous or unsafe callback-route configuration.
- Verify root/custom/prefixed callback routing and complete the real Keycloak browser flow through permissions, CSRF rejection and local logout with an unpublished candidate.

## 0.2.1

- Replace fallback OIDC scopes when a shell explicitly selects scopes; retain documented defaults only on omission.
- Reject empty, malformed, duplicate and invalid scope selections without silently requesting additional permissions.
- Apply the same explicit-selection semantics to supported locales; add JSON-to-CShells-to-OIDC binding regressions.
- Known limitation: the real BFF/Keycloak rehearsal reaches login but cannot route `/signin-oidc` into the shell. This separate callback-routing defect remains unresolved; this release does not establish end-to-end BFF interoperability.

## 0.2.0

- Add immutable shell-local response security policies, deterministic endpoint/feature selection,
  final header ownership, and explicit private/public cache behavior (#1).
- Add typed strict/tolerant JSON profiles, controlled code extensions, optional shell/HTTP integration,
  raw-number compatibility primitives, and versioned RFC 8785 canonicalization (#2).
- Add fixed semantic hosted pages, constrained public branding, manifest/hash/content admission,
  retained immutable asset revisions, and Vite dependency delivery (#3).
- Validate the 25-package family together; run focused validators in Release and fix the flat Host Dockerfile path.

## 0.1.0

- Extract the reusable .NET runtime building blocks from Program Kit.
- Rename the package family from `ProgramKit.*` to `Orbyss.Foundation.*`.
- Establish independent CI, NuGet publication, and host-image publication.
