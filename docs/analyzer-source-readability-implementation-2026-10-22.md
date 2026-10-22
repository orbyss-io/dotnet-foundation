# Source-readability analyzer implementation

This source change starts from `77d950df3ed0ec2e45fd9008272317ecaa981f22`
(VERSION `0.3.6`). It does not select a new release version, update public package
pins, tag, push, publish or claim qualification of a public package. Foundation
remains independent of Program Kit.

## Contract

The existing analyzer package gains an explicit `source-readability-v1` capability.
Its packaged buildTransitive props expose `FoundationSourceReadabilityContract`
and make `FoundationSourceReadability` compiler-visible. Set the latter to `true`
to require ORB1007–ORB1010 as Errors. The packaged JSON marker identifies the same
contract and diagnostic IDs. Existing unrelated Foundation source profiles retain
their current selection; adoption in a consumer is a repair-before-resume event,
not a debt waiver. No generated-code baseline or authored-source exclusion was added.

- ORB1007: lists over two items, and already split nonempty lists, require separate
  parameter/argument lines and an aligned closing parenthesis. Includes primary/base
  constructors, delegates and lambdas.
- ORB1008: prepare runtime calls, object/array/collection construction and deferred
  bodies outside arguments. Real compile-time `nameof` is exempt; `@nameof` methods
  are runtime calls. Method groups and previously prepared values remain usable.
- ORB1009: a chain's receiver and invocation operators occupy separate indented
  lines. Includes explicit/bare/delegate factories, conditional access, parentheses,
  intervening and trailing properties. A factory receiver can be prepared separately.
- ORB1010: fields, constants and field-like events precede other type members.

The old ORB1002 recommendation remains Info. No width recommendation, conditional
language feature preference, or named-argument recommendation was promoted to Error.
The existing SDK owns common unobserved-task warnings and ValueTask misuse diagnostics;
this change does not claim to certify task ownership, cancellation, cleanup or deadlines.
Constructor/member ordering, private helper first-use order, documentation meaning and
coherent abstraction remain review obligations. No automatic code fix changes evaluation
order, deferred activation, cancellation or disposal.

## Focused proof

`dotnet restore tests/analyzer-readability-probe/AnalyzerReadabilityProbe.csproj --locked-mode --nologo`
and `python tests/validate_analyzer.py` are the reproducible focused entrypoints.
The latter preserves existing structural/documentation/identity controls, runs 47
separately compiled source cases and exercises a real local pack/install/build journey:
all four mandatory errors reject bad authored source, and repaired source builds.
The native project asserts the imported capability property, uses a private candidate
cache and local-only feed, and never mistakes compiler/analyzer crashes for rejection.
The fixtures also compile all new owning C# source under the selected contract.

The separately maintained Program Kit Roslyn syntax oracle was copied byte-for-byte
into ignored local test artifacts, not referenced by Foundation source or tests. Oracle
SHA-256: `55e4214d9b81b7aa94c571f01326f46a4bd5d38916f61553df9d13af3310d1d4`.
Its scan found no occurrences in the new production analyzer source. Historical
Foundation source was inventoried, not automatically refactored or called conforming.
The copied diagnostic project's existing MSB3539 intermediate-path warning was retained.

Original failures were inspected and repaired: sandboxed restore could not obtain
vulnerability metadata (NU1900); the same pinned locked restore succeeded with external
process/network access, without suppression. The first native positive fixture lacked
XML-documentation parsing; enabling GenerateDocumentationFile fixed its ORB1005 setup
failure. New controls exposed and repaired whole-chain wrapper action ownership. The
owned-source control initially caught one real misaligned closing parenthesis, then
passed after correction; its multi-file compilation retains internal fixture accessibility.

## Release and adoption handoff

Parent review additionally reproduced a false positive for a multiline prepared parenthesized
receiver and missing prepared-value diagnostics for record `with` and `stackalloc`. The owner
now retains a no-call receiver's whole parentheses span/outer indentation and handles all three
constructed forms. Focused validation passes 54 separately compiled cases plus native package
reject/repair. Earlier 47-case qualification and package hashes are superseded for current use;
retained logs preserve those earlier source states.

The development nupkg under `artifacts/readability-candidate/` uses the unchanged source
version only in an isolated feed/cache. It is not replacement publication bytes for the
existing public version. `source-manifest.json` there binds the actual changed working
files and candidate hash for connected development tests; it is diagnostic evidence,
not release authority. Program Kit may test this explicit local artifact without changing
its publisher capture or default public pin. Until a qualified Foundation release is
published and adopted, the old package must fail the new capability check explicitly.

Selecting a real release candidate requires the Foundation maintenance workflow,
appropriate deterministic and package validation, a new authoritative release version
and separate user publication authority. No dependency survey, runtime lock refresh,
full Host/browser/database matrix or public qualification was performed in this focused
analyzer implementation.
