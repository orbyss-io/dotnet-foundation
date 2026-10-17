// Versioned public browser contract. Shared state is local to this window and
// application surface, including controls loaded by independently bundled code.
const storageKey = Symbol.for('Orbyss.Foundation.Web.Presentation.v1');
const surfaces = globalThis[storageKey] ??= new Map();
const modes = new Set(['never', 'when-unsaved', 'always']);

function surfaceRecord(surface) {
  if (typeof surface !== 'string' || !surface.trim() || surface.length > 256 || /[\u0000-\u001f\u007f]/u.test(surface)) {
    throw new TypeError('A stable application surface identity is required.');
  }
  if (!surfaces.has(surface)) {
    surfaces.set(surface, { sources: new Map(), revision: 0, evaluating: false,
      controls: new Set(), mode: null, active: null });
  }
  return surfaces.get(surface);
}

function evaluate(record) {
  if (record.evaluating) { record.reentrant = true; return 'unknown'; }
  record.evaluating = true;
  record.reentrant = false;
  const revision = record.revision;
  let dirty = false;
  let unknown = false;
  try {
    // A source cannot extend this iteration indefinitely by registering sources.
    for (const source of [...record.sources.values()]) {
      let state;
      try { state = source.getState(); } catch { state = 'unknown'; }
      if (state === 'dirty') dirty = true;
      else if (state !== 'clean') unknown = true;
    }
    if (revision !== record.revision || record.reentrant || unknown) return 'unknown';
    return dirty ? 'dirty' : 'clean';
  } finally {
    record.evaluating = false;
  }
}

/** Register current local state only; never save, discard or query a provider. */
export function unsavedWork(surface) {
  const record = surfaceRecord(surface);
  return Object.freeze({
    register(source) {
      const identity = {};
      record.sources.set(identity, source);
      record.revision++;
      return () => {
        if (record.sources.delete(identity)) record.revision++;
      };
    },
    getState() { return evaluate(record); },
  });
}

/** The native dialog owns focus, keyboard cancellation and its fresh Confirm click. */
export function showSignOutConfirmation({ onConfirm, onCancel, copy = {}, returnFocus }) {
  const previous = returnFocus ?? document.activeElement;
  const dialog = document.createElement('dialog');
  const title = document.createElement('h2');
  const description = document.createElement('p');
  const cancel = document.createElement('button');
  const confirm = document.createElement('button');
  const identity = 'foundation-sign-out-' + crypto.randomUUID();
  title.id = identity + '-title';
  description.id = identity + '-description';
  title.textContent = copy.title ?? 'Sign out?';
  description.textContent = copy.description ?? 'Some work may be unsaved or still saving. Sign out anyway?';
  cancel.textContent = copy.cancel ?? 'Keep working';
  confirm.textContent = copy.confirm ?? 'Sign out';
  cancel.type = confirm.type = 'button';
  cancel.autofocus = true;
  dialog.setAttribute('aria-labelledby', title.id);
  dialog.setAttribute('aria-describedby', description.id);
  dialog.append(title, description, cancel, confirm);
  let closed = false;
  function finish(action) {
    if (closed) return;
    closed = true;
    dialog.close();
    dialog.remove();
    if (previous?.isConnected) previous.focus();
    // Dispatch remains on the Confirm event's synchronous call stack. Awaiting
    // a dialog promise here would lose popup/user-gesture protocol guarantees.
    action();
  }
  cancel.addEventListener('click', () => finish(onCancel));
  confirm.addEventListener('click', () => finish(onConfirm));
  dialog.addEventListener('cancel', event => { event.preventDefault(); finish(onCancel); });
  dialog.addEventListener('close', () => finish(onCancel));
  document.body.append(dialog);
  try { dialog.showModal(); } catch (error) { dialog.remove(); throw error; }
  return { focus() { cancel.focus(); }, dismiss() { finish(() => {}); } };
}

/** Coordinate user-requested sign-out; automatic expiry uses separate recovery. */
export function createSignOutControl({ surface, mode = 'when-unsaved', signOut,
  confirmation = showSignOutConfirmation, copy }) {
  if (!modes.has(mode)) throw new TypeError('Unknown sign-out confirmation mode.');
  if (typeof signOut !== 'function' || typeof confirmation !== 'function') {
    throw new TypeError('A secure sign-out action and presentation callback are required.');
  }
  const record = surfaceRecord(surface);
  if (record.mode !== null && record.mode !== mode) throw new TypeError('Sign-out controls in one application surface must share a confirmation policy.');
  const owner = {};
  record.controls.add(owner);
  record.mode = mode;
  let disposed = false;
  function releasePolicy() {
    if (!record.controls.size && !record.active) record.mode = null;
  }
  return Object.freeze({
    request({ returnFocus } = {}) {
      if (disposed) return Promise.reject(new Error('Sign-out control is disposed.'));
      if (record.active) {
        record.active.view?.focus();
        return record.active.promise;
      }
      let resolve;
      let reject;
      const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
      const active = { owner, promise, view: null, dispatched: false, settled: false, cancel: null };
      record.active = active;
      function settle(failed, value) {
        if (active.settled) return;
        active.settled = true;
        let cleanupError;
        let cleanupFailed = false;
        try { active.view?.dismiss(); } catch (error) { cleanupFailed = true; cleanupError = error; }
        if (record.active === active) record.active = null;
        releasePolicy();
        if (cleanupFailed && failed) {
          reject(new AggregateError([value, cleanupError], 'Sign-out and presentation cleanup failed.', { cause: value }));
        } else if (cleanupFailed) reject(cleanupError);
        else if (failed) reject(value);
        else resolve(value);
      }
      active.cancel = () => {
        if (!active.dispatched) settle(false, { status: 'cancelled', dispatched: false });
      };
      function showConfirmation() {
        active.view = confirmation({ onConfirm: () => dispatch(true), onCancel: active.cancel, copy, returnFocus });
        if (active.settled || active.dispatched) active.view?.dismiss();
      }
      function dispatch(confirmed = false) {
        if (active.settled || active.dispatched) return;
        // A displayed dialog never becomes proof of a later clean snapshot.
        // Confirmation authorizes the current sign-out, even if state changes.
        try {
          const state = mode === 'never' ? null : evaluate(record);
          if (!confirmed && mode === 'when-unsaved' && state !== 'clean') {
            showConfirmation();
            return;
          }
          active.view?.dismiss();
          active.dispatched = true;
          const result = signOut(); // Synchronous invocation preserves the gesture.
          Promise.resolve(result).then(value => settle(false, { status: 'dispatched', result: value }),
            error => settle(true, error));
        } catch (error) { settle(true, error); }
      }
      try {
        if (mode === 'always') showConfirmation();
        else dispatch();
      } catch (error) { settle(true, error); }
      return promise;
    },
    dispose() {
      if (disposed) return;
      disposed = true;
      record.controls.delete(owner);
      if (record.active?.owner === owner) record.active.cancel();
      releasePolicy();
    },
  });
}
