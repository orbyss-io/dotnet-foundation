// Adapter/model tests; selected SPA client and provider conformance are separate scopes.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createSpaSignOutControl } from '../../src/Orbyss.Foundation.Web.Presentation/browser/spa.mjs';

test('SPA clears and observes local state before same-stack provider dispatch', async () => {
  const effects = [];
  let state = 'authenticated';
  const control = createSpaSignOutControl({ surface: crypto.randomUUID(), mode: 'never', adapter: {
    clearLocalAuthentication() { effects.push('clear'); state = 'anonymous'; },
    readLocalSession() { effects.push('observe'); return state; },
    beginProviderLogout() { effects.push('provider'); },
  } });
  const result = control.request();
  assert.deepEqual(effects, ['clear', 'observe', 'provider']);
  assert.deepEqual(await result, { status: 'dispatched', result: { localSession: 'anonymous', provider: 'dispatched' } });
  control.dispose();
});
for (const failure of [() => { throw new Error('unavailable'); }, () => Promise.reject(new Error('unavailable'))]) {
  test('SPA provider failure retains observed local termination without retry', async () => {
    let count = 0;
    const control = createSpaSignOutControl({ surface: crypto.randomUUID(), mode: 'never', adapter: {
      clearLocalAuthentication() {}, readLocalSession: () => 'anonymous', beginProviderLogout() { count++; return failure(); },
    } });
    assert.deepEqual(await control.request(), { status: 'dispatched', result: { localSession: 'anonymous', provider: 'unavailable' } });
    assert.equal(count, 1);
    control.dispose();
  });
}
for (const state of ['authenticated', 'unknown', undefined]) {
  test('SPA refuses provider dispatch without observed anonymous local state: ' + state, async () => {
    let calls = 0;
    const control = createSpaSignOutControl({ surface: crypto.randomUUID(), mode: 'never', adapter: {
      clearLocalAuthentication() {}, readLocalSession: () => state, beginProviderLogout() { calls++; },
    } });
    await assert.rejects(control.request(), /not observed/u);
    assert.equal(calls, 0); control.dispose();
  });
}
test('SPA rejects async local clearing before contacting a provider', async () => {
  let calls = 0;
  const control = createSpaSignOutControl({ surface: crypto.randomUUID(), mode: 'never', adapter: {
    clearLocalAuthentication: () => Promise.reject(new Error('failed clear')), readLocalSession: () => 'anonymous', beginProviderLogout() { calls++; },
  } });
  await assert.rejects(control.request(), /synchronously/u);
  assert.equal(calls, 0); control.dispose();
});
