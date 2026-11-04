# Consumer regression repairs

Foundation 0.3.14 consolidates two demonstrated native requirements. Public availability and
downstream compatibility remain the official release and consumer qualification outcomes.

- Configuration removal is separately resolved under the existing explicit Keycloak Configuration
  capability. The published update interface remains unchanged. Scope removal selects one exact
  default or optional assignment. Client-key removal validates both bounded selections before I/O,
  reads fresh maps, preserves unselected attributes/flow bindings, emits no credential/scalar replay,
  sends an empty map when its final key is deliberately removed, and converges without a repeat write.
- Dynamic-page resolution and precommit verification can explicitly return SessionExpired. The native
  bounded Problem writer returns 401/authentication_required; forbidden and unavailable retain 403/503.
  No resource bytes escape a rejected post-render admission, and this result does not clear an
  independently valid replacement BFF session. No parallel Problem writer or lifetime policy is added.

Existing ApplicationName/theme, recovery renderer, session-boundary observer and confirmation seams
already cover the presentation requirements. Those need consumer examples and integration proof,
not another native lifecycle. Foundation remains independent of consumer/toolkit source.

Local source validation used the installed .NET10.0.403 SDK and Python3.15 through the supported
FoundationMetadataPython selector: locked solution restore, Release solution build (0 warnings/errors),
the administration probe, and the live native dynamic-page/BFF probe. The build took 67.62 seconds;
restore took 2.93 seconds and administration 1.30 seconds. The dynamic-page result is retained in local
diagnostics. The removal probe includes invalid/control/duplicate/oversized selections rejected before
any request, exact escaped DELETE paths, unrelated MFA preservation, last-key empty maps, retry no-change,
native shell/standalone registration, and omission of unrelated capabilities.

Original shared-compiler pipe timeouts, an overlapping diagnostic-build file lock, missing metadata
Python in a child PATH, and sandbox-denied loopback remain preserved local failures. Supported reruns
use command-local compiler/node settings, the existing explicit Python selector and scoped loopback
access. Product assertions and device toolchains were unchanged. Linux/Docker/browser/image-security
qualification is performed by the maintained official workflows; local focused tests do not replace it.
