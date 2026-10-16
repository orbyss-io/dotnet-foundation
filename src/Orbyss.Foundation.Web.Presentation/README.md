# Browser presentation contract under development

The shared browser module provides window-local, per-application unsaved-work
registration and user-requested sign-out confirmation. `presentation.mjs` and its
matching `presentation.d.mts` use the same direct ESM import path under strict
NodeNext resolution. Each editable component registers a synchronous current
`clean`, `dirty` or `unknown` source and disposes it with that component. Pending
saves are unknown. Registration never saves, discards or contacts a provider.

Controls in one surface share a confirmation policy and one pending request.
`when-unsaved` is the default; `never` bypasses source evaluation, and `always`
confirms even a clean aggregate. Pass the invoking element as
`request({ returnFocus: element })`, including browsers where pointer activation
does not focus a button. Cancel preserves local state. Confirm invokes the supplied
secure action on its fresh synchronous event stack. Concurrent controls do not
duplicate a request. A dispatched result describes protocol invocation, not proof
that either local or provider sign-out completed.

Sixteen model checks and real Chromium/WebKit module probes cover this contract.
The browser probe uses a fixture action, with no authentication/provider operation.
Native feature/routes/options, transport binding, packaged assets and configured
application acceptance remain implementation work; this directory is not yet a
published or selectable Foundation feature.
