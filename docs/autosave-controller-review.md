# Autosave controller first review

30 October 2026. Branch `codex/reusable-autosave`, based on
`bc66af8d7a707b4d2ae1f199dfdcdfd3a428f07a`. F1 is implemented for human review.
The requester reviewed F1 and authorized the two repairs, remaining qualification, public publication
and Program Kit adoption. The repairs and native asset serving are implemented. Native source probes,
strict types and the Chromium scenario pass locally. WebKit is unavailable at the selected local
Playwright path; Firefox has the known Windows launch limitation. Both remain in CI. Exact candidate
maintenance, package/CI evidence, public publication and Program Kit adoption remain open.
No consumer is updated by this increment.

## Inspect the working behavior

The public [module](../src/Orbyss.Foundation.Web.Presentation/browser/autosave.mjs) and
[declarations](../src/Orbyss.Foundation.Web.Presentation/browser/autosave.d.mts) supply one editor-local
controller. A target registers its application adapter. A manual request or interval reserves the
same active slot, admits the application context, captures an immutable current request and invokes
the writer. An accepted receipt acknowledges only the captured edit revision. The next interval can
visit other eligible targets without changing a conflicted or uncertain target's original attempt.

The two independent [recording adapters](../tests/browser/fixtures/autosave-adapters.mjs) demonstrate
different policies. `VersionConflictAdapter` compares a captured server version and requires explicit
comparison/reapplication after conflict. `OverwriteAdapter` contains no server-version field and
replaces current content on a new explicit action. Both retain one receipt for identical replay and
refuse changed bytes under an existing key. These are fictional in-memory stores, not production
database or authentication implementations.

Review these paths in the [focused cases](../tests/browser/autosave.test.mjs):

- Initial cadence, active typing, manual save with autosave disabled and clean ticks.
- One pass over metadata and dependent frames; targets registered during a pass wait for the next.
- Edit 7 in flight while edit 8 is entered: acknowledgement keeps edit 8 dirty.
- Response lost after a recording effect: the exact original payload/key survives, and replay has one effect.
- A timed-out writer that ignores abort: the user observes unknown promptly, but another writer remains
  busy until the original call settles. A late confirmation does not overwrite the cutoff observation.
- Suspension, account-lifetime disposal, unregister/re-register and callback reentrancy cannot publish
  an old continuation into its replacement.
- A failed application confirmation callback retains the established commit and pauses the adapter.
  It never licenses another write. Invalid async callbacks are observed without unhandled rejection.

## Commands and observed scope

From this Foundation checkout:

```powershell
node --test tests/browser/autosave.test.mjs tests/browser/presentation.test.mjs tests/browser/spa-presentation.test.mjs
```

Result: **67 passed**, comprising 42 autosave cases and 25 existing presentation/SPA cases, in
approximately 0.15 seconds for the process. No cases failed, skipped or were cancelled.

The focused strict declaration check also passed:

```powershell
node C:/Users/tech_/Code/Orbyss/dotnet-foundation/tests/browser/node_modules/typescript/bin/tsc -p tests/browser/tsconfig.autosave.json
```

The command uses the existing prepared Foundation TypeScript installation without copying it or
installing dependencies. The maintained npm entrypoints are `test:autosave` and `test:autosave:types`;
the ordinary browser type/module scripts include the new cases. The strict type cases reject mutable
pending bytes, asynchronous callbacks, unsafe status classifications and direct transport bypass.

Both streams, commands, elapsed times and selected source hashes are retained in
[the local diagnostic run](../artifacts/autosave-f1/20261030T091329Z/results.json).
The deterministic clock replaces built-ins only inside tests; production exposes no scheduler extension.
These checks establish controller semantics and declaration consistency. They do not establish native
asset serving, an installed package, browser lifecycle behavior, database commit guarantees or a release.

## Source review findings and adjustments

Adapter methods are snapshotted and bound to their original object, so class-based adapters retain
their state. The initial implementation's plain object spread would have lost prototype methods;
the actual two class adapters now exercise that integration. Normal request completion is returned
after releasing the active slot, allowing sequential awaited manual saves. Cutoff reporting remains
separate from drain, so an uncooperative writer cannot cause overlapping requests.

No new dependency, .NET project, host owner, wire schema, storage backend or domain version policy was
introduced. Required intervals and transport budgets remain application configuration. The package
bootstrap and route list are unchanged in F1. The project already packs browser files, but the new
native asset routes and package/Host evidence are deliberately still due in F2.

One design statement needs this precision: a finite pass visits a bounded snapshot of targets, but
actual settlement cannot be bounded when an adapter ignores cancellation. The finite operation budget
bounds the reported cutoff; the pass/drain stays pending until that adapter settles. Releasing the slot
at timeout would violate the accepted no-overlap requirement.

## Walkthrough observations before continuing

The follow-up walkthrough originally reproduced two gaps without changing runtime source. The archived diagnostic
[`review-walkthrough.mjs`](../artifacts/autosave-f1/review-walkthrough.mjs) records selected source hashes
and actual observations in `review-observations.json`. Its assertions characterize the earlier source;
they are not current acceptance criteria. `review-repair-before/` preserves the original failing
regressions. The maintained cases now require successful same-pass dependency saving and activity
notification at timeout/drain completion. Source changes preserve registration order and expose
read-only controller activity; additional cases challenge observer-driven disposal and disposed drain.

**Dependency order:** registration order saves metadata before a dependent frame in the first pass.
Rotation reverses that order in the second pass. When both need saving and the frame needs the new
metadata version, the frame is skipped before metadata succeeds and remains dirty until the following
pass. At a twenty-minute interval, that can defer an otherwise ready frame by another interval.
Resolve this before adoption: the smallest repair is to preserve the application's deliberate target
order on every pass instead of rotating it. Metadata can remain registered before frames, while the
adapter still checks actual parent readiness. Retain one visit per target without unbounded retries;
add an explicit dependency graph only if a concrete integration needs more than stable ordering.
This is a proposed contract refinement, not a claim that the current controller enforces dependencies.

**Drain observability:** after timeout, the active target reports unknown and another target's request
returns busy. Once the original promise settles, the slot is released, but no target notification
announces that change. The public controller also has no readable busy/draining state. Add a small
read-only controller activity observation so UI can explain waiting and re-enable controls without
polling or probing by attempting another save. Keep this separate from the target's business outcome;
drain must never be presented as proof that a server operation stopped or rolled back.

The editor-wide slot remaining occupied by an uncooperative adapter is an intentional safety/availability
tradeoff. Do not resolve it by releasing at timeout. Production adapters need cooperating bounded I/O;
the activity observation should explain a prolonged wait. Native/browser/package evidence is still
required after these source/API review issues are resolved.

## Remaining delivery and next review decision

The remaining authorized objective is to finish exact candidate packaging/browser checks, publicly publish new
Foundation packages through its existing maintenance/release procedure, and have Program Kit qualify
and adopt those exact public packages in its dependency profile. This F1 result does not complete that
objective or claim published availability.

dezaaglijst's update is deferred until the requester discusses it after the public-package/profile
milestone. Its source, installed toolkit and selected dependencies remain untouched. Existing review
decisions, frozen profiles and unrelated worktrees remain preserved.
