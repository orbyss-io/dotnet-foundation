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


## Independent review repair

The concurrent owner identified three blockers in 0cd7947: current Json uses a const
initializer and 21-source inventory; conditional parsing ignored compiler symbols;
assembly provenance hashed obj while NuGet normally packs bin. Repair is isolated on
codex/settings-metadata-companion-repair, based on the owner's committed runtime source
6d9c1b73b0428ec24f6481779958e6c7449d7501 plus cherry-picked companion 27f544c.
The original pushed candidate remains intact. No dirty owner files are imported.

Constant evaluation uses compiler language version/references without runtime execution.
Conditional/preprocessor source is explicitly rejected instead of ignoring compiler symbols.
Final TargetPath and NuGet's actual FinalOutputPath are checked; obj is no longer the
assembly authority. Json's current full source inventory is reviewed/rebound, while the
contract still claims only JsonProfileSettings, not new ASP.NET/provider/Host settings.
Focused repair acceptance pending. Build 0.1.0 remains immutable; 0.2.0 unpublished.

Repair acceptance completed with exact final package hashes verified against results:
- artifacts/settings-build/tmpkv37fu7i/results.json: 23 base rejection cases, eight finite
  limit negatives, real constant default7, FEATURE compiler default9 with metadata rejection,
  bin-only mutation rejection, skipped build/direct target rejection, design-time rejection,
  later no-build packing rejection, fresh direct compilation default4 and final packed DLL hash.
  Metadata/packed output remains unchanged on rejection; no publisher initialization executes.
- artifacts/feature-build/tmpj2up7yc7/descriptor-validation.json: all 31 legacy/current
  descriptor negatives preserve output and valid schemas1/2 pack with this same final nupkg.
- artifacts/settings-owner/tmp2ah8l2io/results.json: actual current Json default values,
  admission, unknown extension denial and final DLL binding match the packed owner.
- artifacts/settings-metadata/repair-evidence.json binds final Build/Json/Collections source
  archives. These are private source candidates, not available or republished versions.
- Program Kit repair integration validates the actual bounded publisher schema and exact
  selected Json/Collections packages and runs the stdlib verifier with Python -I outside source.
- Program Kit bounded Development: 70/70 pass, chromium,webkit, journal
  artifacts/validation-runs/20261006T120358Z-02d878c1/journal.json (in Program Kit).
  Source/receiver metadata count/default constraints remain explicit and W3 remains partial.

The initial design-time fixture assertion was overly strict about unchanged bin bytes:
design-time SDK compilation can produce a DLL, but cannot refresh metadata here. Its failed
run remains at artifacts/settings-build/tmpjz6wa_hw/. Corrected acceptance preserves metadata
and packed output, rejects no-build publication, and then compiles fresh source through the
direct target. The earlier old23-case artifacts remain available alongside final evidence.

The concurrent owner's newer runtime fixes after6d9 must be overlaid and reviewed before
its runtime/Host/F6 release qualification. Only this companion amendment is proposed here;
no full-framework coverage, new provider/ASP.NET/Host metadata, publication, stable tag,
paid worker, complete Release suite or original/dirty consumer modification is claimed.


## Expansion/allocation review follow-on

The owner independently found that5ef85c8 encoded the entire JSON string before checking
2 MiB and used unbounded ReadAllBytes for assembly hashes. Preserve that candidate; repair
now uses fixed-buffer encoding, per-setting cumulative admission before retaining more DTO
nodes, typed constant JsonValues without duplicated encoded elements, and streamed bounded
assembly hashes. Constraints and existing receipt byte reads are bounded as well.
Installed expansion and sparse assembly-size acceptance pending. The initial build correctly
rejected a nested helper type via ORB1003/ORB1004; the buffer was moved to its own source file
and private helper order corrected, without suppressing analyzers.
Runtime owner has newer cf495f7 source; overlay/requalify before publication remains its job.

Final expansion evidence sealed against exact streaming package bytes:
- artifacts/settings-build/tmp5hkdpj78/results.json: all prior base/review/limit tests pass,
  plus admitted source17,925bytes/declaration1,256bytes expanding to4,194,304 default bytes,
  and source287,203bytes/declaration64,036bytes expanding to1,061,158,912 default bytes.
  Both reject inside the fixed2,097,152-byte encoding buffer, without buffer growth,
  OutOfMemory/MSB4018, owner initialization or replacing metadata/packed output.
  Sparse final DLL length268,435,457 rejects before streamed hashing at256MiB cap.
- artifacts/feature-build/tmpl00xi6k5/descriptor-validation.json: 31 descriptor negatives
  and both supported schemas pass with this exact final Build archive.
- artifacts/settings-owner/tmpx0udxd2a/results.json: current Json default/admission and
  assembly-byte equality pass with streaming output generation.
- Program Kit settings-streaming-integration.log: actual final Json/Collections/compiler
  schema metadata passes native package/hash/assembly checks and independent Python-I receiver.
  No Program Kit product code changes in this follow-on; its prior70/70 bounded Development
  evidence remains applicable to unchanged receiver code. No new Release receipt is implied.

The buffer stores exactly one2MiB encoding allocation, reused for constraints/admission/final
output. Incremental per-setting encoding bounds retained DTO expansion before another setting
is processed; typed JsonValues share primitive strings without pre-encoding each JsonElement.
Final output copying is also bounded, and source/assembly receipts admit finite file lengths.
No ToJsonString/unbounded ReadAllBytes remains on output/assembly processing. Older candidates
5ef85c8 and0cd7947 remain preserved; this follow-on is required before integration/publication.
The runtime owner's newer cf495f7 fixes still require its source overlay and full qualification.
