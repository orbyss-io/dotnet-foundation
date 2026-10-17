// Admission/model tests only. Real native-host pages, keyboard/focus and popup
// protocols require the separate browser integration qualification.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createSignOutControl, unsavedWork } from '../../src/Orbyss.Foundation.Web.Presentation/browser/presentation.mjs';
import { loadBrowserPresentation } from '../../src/Orbyss.Foundation.Web.Presentation/browser/configuration.mjs';

test('native configuration admits exact nested BFF and client-owned contracts without protocol acquisition', async () => {
  const priorFetch = globalThis.fetch; const priorLocation = globalThis.location;
  const value = { contract: 'foundation-browser-presentation/v1', surface: '/nested/app', signOutConfirmation: 'when-unsaved',
    modulePath: '/nested/app/account/assets/v1/presentation.mjs', bffModulePath: '/nested/app/account/assets/v1/bff.mjs',
    antiforgeryPath: '/nested/app/bff/antiforgery', logoutPath: '/nested/app/bff/logout' };
  const calls = [];
  globalThis.location = new URL('https://application.example/nested/app/');
  globalThis.fetch = async (path, options) => { calls.push({ path, options }); return Response.json(value); };
  try {
    const selected = await loadBrowserPresentation('/nested/app/account/configuration');
    assert.deepEqual(selected, value); assert(Object.isFrozen(selected)); assert.equal(calls.length, 1);
    assert.equal(calls[0].path, '/nested/app/account/configuration');
    assert.equal(calls[0].options.redirect, 'error'); assert.equal(calls[0].options.cache, 'no-store');
    value.bffModulePath = value.antiforgeryPath = value.logoutPath = null;
    assert.equal((await loadBrowserPresentation('/nested/app/account/configuration')).bffModulePath, null);
  } finally { globalThis.fetch = priorFetch; if (priorLocation === undefined) delete globalThis.location; else globalThis.location = priorLocation; }
});

test('native configuration refuses route substitution, mixed transports, excess fields and oversized bodies', async () => {
  const priorFetch = globalThis.fetch; const priorLocation = globalThis.location;
  const value = { contract: 'foundation-browser-presentation/v1', surface: '/', signOutConfirmation: 'always',
    modulePath: '/auth/assets/v1/presentation.mjs', bffModulePath: null, antiforgeryPath: null, logoutPath: null };
  globalThis.location = new URL('https://application.example/');
  try {
    let calls = 0;
    globalThis.fetch = async () => { calls++; return Response.json(value); };
    for (const path of ['https://other.example/auth/configuration', '//other/auth/configuration', '/a/../auth/configuration', '/auth/configuration?next=other'])
      await assert.rejects(loadBrowserPresentation(path), TypeError);
    assert.equal(calls, 0);
    for (const patch of [{ modulePath: 'https://other.example/presentation.mjs' }, { surface: '/foreign' },
      { antiforgeryPath: '/bff/antiforgery' }, { authenticated: true }, { signOutConfirmation: 'sometimes' }]) {
      globalThis.fetch = async () => Response.json({ ...value, ...patch });
      await assert.rejects(loadBrowserPresentation('/auth/configuration'), TypeError);
    }
    globalThis.fetch = async () => new Response(' '.repeat(16385), { headers: { 'content-type': 'application/json' } });
    await assert.rejects(loadBrowserPresentation('/auth/configuration'), /read budget/u);
    globalThis.fetch = async () => new Response('{}', { status: 503, headers: { 'content-type': 'application/json' } });
    await assert.rejects(loadBrowserPresentation('/auth/configuration'), /unavailable/u);
    const cancelled = new AbortController(); cancelled.abort();
    await assert.rejects(loadBrowserPresentation('/auth/configuration', { signal: cancelled.signal }), { name: 'AbortError' });
  } finally { globalThis.fetch = priorFetch; if (priorLocation === undefined) delete globalThis.location; else globalThis.location = priorLocation; }
});

function fixture(mode = 'when-unsaved') {
  const surface = crypto.randomUUID();
  const registry = unsavedWork(surface);
  const effects = [];
  let dialog;
  const options = { surface, mode, signOut() { effects.push('dispatch'); return 'observed-protocol-result'; },
    confirmation(request) {
      effects.push('dialog');
      dialog = request;
      return { dismiss() {}, focus() { effects.push('focus'); } };
    } };
  return { surface, registry, effects, options, dialog: () => dialog, control: createSignOutControl(options) };
}

test('clean/no-source sign-out invokes the protocol synchronously in the original call', async () => {
  const f = fixture();
  const request = f.control.request();
  assert.deepEqual(f.effects, ['dispatch']);
  assert.deepEqual(await request, { status: 'dispatched', result: 'observed-protocol-result' });
  f.control.dispose();
});

test('never bypasses every source and confirmation evaluation', async () => {
  const f = fixture('never');
  const dispose = f.registry.register({ getState() { throw new Error('must never evaluate'); } });
  await f.control.request();
  assert.deepEqual(f.effects, ['dispatch']);
  dispose(); f.control.dispose();
});

test('always confirms even with no sources; cancellation dispatches nothing', async () => {
  const f = fixture('always');
  const request = f.control.request();
  assert.deepEqual(f.effects, ['dialog']);
  f.dialog().onCancel();
  assert.deepEqual(await request, { status: 'cancelled', dispatched: false });
  assert.deepEqual(f.effects, ['dialog']);
  f.control.dispose();
});

for (const [label, source] of [
  ['dirty', { getState: () => 'dirty' }], ['unknown save', { getState: () => 'unknown' }],
  ['invalid state', { getState: () => 'saved-maybe' }], ['asynchronous state', { getState: () => Promise.resolve('clean') }],
  ['throwing state', { getState() { throw new Error('uncertain save'); } }], ['invalid source', null],
]) {
  test(label + ' requires confirmation with zero protocol effects before Confirm', async () => {
    const f = fixture();
    const dispose = f.registry.register(source);
    const request = f.control.request();
    assert.deepEqual(f.effects, ['dialog']);
    f.dialog().onConfirm();
    assert.deepEqual(f.effects, ['dialog', 'dispatch']);
    assert.equal((await request).status, 'dispatched');
    dispose(); f.control.dispose();
  });
}

test('multiple sources and disposed components aggregate without stale warnings', () => {
  const surface = crypto.randomUUID();
  const registry = unsavedWork(surface);
  const clean = registry.register({ getState: () => 'clean' });
  const dirty = registry.register({ getState: () => 'dirty' });
  assert.equal(registry.getState(), 'dirty');
  dirty(); dirty();
  assert.equal(registry.getState(), 'clean');
  const uncertain = registry.register({ getState: () => 'unknown' });
  assert.equal(registry.getState(), 'unknown');
  uncertain(); clean();
  assert.equal(registry.getState(), 'clean');
});

test('independent bundles share one surface registry and pending sign-out', async () => {
  const other = await import('../../src/Orbyss.Foundation.Web.Presentation/browser/presentation.mjs?independent-bundle');
  const f = fixture();
  const dispose = other.unsavedWork(f.surface).register({ getState: () => 'dirty' });
  assert.equal(f.registry.getState(), 'dirty');
  const second = other.createSignOutControl({ ...f.options, signOut() { throw new Error('duplicate dispatch'); } });
  const first = f.control.request();
  assert.equal(first, second.request());
  assert.deepEqual(f.effects, ['dialog', 'focus']);
  f.dialog().onConfirm(); f.dialog().onConfirm();
  await first;
  assert.deepEqual(f.effects, ['dialog', 'focus', 'dispatch']);
  dispose(); second.dispose(); f.control.dispose();
});

test('state changes during the dialog are re-evaluated and cancellation does not dispatch', async () => {
  const f = fixture();
  let state = 'dirty';
  let reads = 0;
  const dispose = f.registry.register({ getState() { reads++; return state; } });
  const pending = f.control.request();
  state = 'unknown';
  f.dialog().onConfirm();
  await pending;
  assert.equal(reads, 2);
  const next = f.control.request();
  f.dialog().onCancel();
  await next;
  assert.equal(f.effects.filter(value => value === 'dispatch').length, 1);
  dispose(); f.control.dispose();
});

test('reentrant or mutating source evaluation cannot assert clean aggregate state', () => {
  const registry = unsavedWork(crypto.randomUUID());
  const first = registry.register({ getState() { registry.getState(); return 'clean'; } });
  assert.equal(registry.getState(), 'unknown');
  first();
  let added;
  const second = registry.register({ getState() { added ??= registry.register({ getState: () => 'dirty' }); return 'clean'; } });
  assert.equal(registry.getState(), 'unknown');
  assert.equal(registry.getState(), 'dirty');
  second(); added();
});

test('protocol failure, including falsy rejection, is preserved and never automatically retried', async () => {
  for (const error of [new Error('provider unavailable'), undefined, null, false]) {
    const f = fixture();
    f.control.dispose();
    let calls = 0;
    const control = createSignOutControl({ ...f.options, signOut() { calls++; return Promise.reject(error); } });
    let observed = Symbol();
    await control.request().catch(value => { observed = value; });
    assert.equal(observed, error);
    assert.equal(calls, 1);
    control.dispose();
  }
});

test('disposal cancels only the owning pending dialog and does not dispatch', async () => {
  const f = fixture('always');
  const pending = f.control.request();
  f.control.dispose();
  assert.deepEqual(await pending, { status: 'cancelled', dispatched: false });
  assert.deepEqual(f.effects, ['dialog']);
  await assert.rejects(f.control.request(), /disposed/);
});

test('one application surface cannot silently mix confirmation policies', () => {
  const f = fixture('always');
  assert.throws(() => createSignOutControl({ ...f.options, mode: 'never' }), /share a confirmation policy/);
  f.control.dispose();
});
