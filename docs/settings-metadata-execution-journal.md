# Settings metadata implementation journal

2026-10-06: User accepted the additive Foundation settings companion after Program Kit
commit 4ed712f was pushed. Work is isolated on codex/settings-metadata-companion at main
ab22740d9bc86f01d3220751182cdcfcef9104ef. Original and concurrent checkouts remain untouched.
Coordination message sent to the concurrent contract chat with explicit user authorization.

Contract: existing Build pipeline, separate versioned settings companion, source-derived
bounded defaults, source-bound owner semantics, no runtime execution or Program Kit dependency.
First real owner is JsonProfileSettings, complete for its named type only; framework-wide
host/shell/Nuplane/authentication coverage remains incomplete. No Release/publication/tag run.
Initial implementation validation follows.

Installed Build candidate 0.2.0 final tests pass: 23 malformed/conflicting/secret/stale rejection
cases preserve packed and metadata output; source default changes derive new values; no
publisher module initializer or property initializer executes. Compilation emits provenance
with actual assembly SHA256. Pack revalidates compiled output; changed declarations/source
with --no-build reject. Existing FeatureDescriptor schemas 1/2 remain separate.

Locally packed Json 0.2.4 is a source artifact only, not a republication or available version.
Real owner probe verifies default equality, both bounds/preset validation and unknown
extension denial against the packed runtime assembly. Program Kit consumes this actual
package through its selected closure and independently verifies the delivered receiver.
Framework host/shell/Nuplane/authentication and other types remain uncovered. Runtime
family publication/versioning must be coordinated with the concurrent owner before merge.
CI and existing runtime/tool release workflows add these acceptance checks before attestation
or publication; no publication trigger/authorization/gate is weakened. No workflow launched.

Final focused evidence:
- Pinned SDK 10.0.202 locked Build and Json restores and Release configuration packs passed.
  This is targeted package qualification, not the complete Release suite.
- artifacts/settings-build/tmp9m8cl22l/results.json: installed Build 0.2.0 candidate,
  23 malformed/source-boundary rejection cases, unchanged output on rejection, defaults
  derived after source changes, partial coverage retained and no publisher startup.
- artifacts/feature-build/tmpsdxeil2n/descriptor-validation.json: 31 existing descriptor
  rejection cases and successful schemas 1/2 with output preservation. Descriptor task/schema
  source remains unchanged; new emitter also packs alongside two real descriptor identities.
- artifacts/settings-owner/tmpzls7kd70/results.json: final actual Json package defaults,
  both admission boundaries/presets, unknown extension denial and packed assembly binding.
- python tests/validate_tool_release.py: 4 independent selection/publication boundary checks pass.
- Program Kit artifacts/handoff-validation/settings-targeted-results.json: schema, scaffold,
  native engineering contract and real companion integration pass. Optional upstream integration
  validates emitted payload against the actual packaged settings schema and runs the portable
  verifier with Python -I outside source/toolkit. No public availability qualification is claimed.

Existing CI, runtime release and independent tool release tests retain original gates and add
settings acceptance. Build 0.2.0 must use its own independent release before consumers select
it. The changed runtime payload requires the authoritative family owner's new candidate/version
qualification; no existing immutable runtime/tool version may be overwritten. Required settings
of host-provided dependencies are not covered by this bundled-package seam; their owners must
supply metadata bound to the exact host/artifact authority. This branch changes no runtime
behavior, Foundation runtime version, real application or external provider repository.
