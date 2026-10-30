// Controller/model evidence only. No server, database, persistence or actual browser acceptance is claimed.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { AUTOSAVE_CONTRACT, createAutosaveController } from '../../src/Orbyss.Foundation.Web.Presentation/browser/autosave.mjs';
import { OverwriteAdapter, VersionConflictAdapter, recordingStore } from './fixtures/autosave-adapters.mjs';
import { Clock, deferred } from './fixtures/autosave-clock.mjs';

function fixture(t, options = {}) {
  const clock = new Clock();
  const controllers = [];
  const gates = [];
  const controller = override => {
    const value = createAutosaveController({ enabled: true, intervalMilliseconds: 100,
      operationTimeoutMilliseconds: 30, ...options, ...override });
    controllers.push(value);
    return value;
  };
  const main = controller();
  const add = (adapter = new OverwriteAdapter(), target = 'target-a', initial = {}) => main.register({ target,
    maximumPayloadBytes: 4096, initial: { editRevision: 1, confirmedEditRevision: 0, generation: 'initial', pending: null, ...initial }, adapter });
  const hold = () => { const gate = deferred(); gates.push(gate); return gate; };
  t.after(async () => {
    try {
      for (const gate of gates) gate.resolve({ kind: 'unknown' });
      await Promise.all(controllers.map(value => value.dispose()));
      assert.equal(clock.timers.size, 0, 'all owned timers are released');
    } finally { clock.restore(); }
  });
  return { clock, controller, main, add, hold };
}

test('optional module and constructor admit policy without effects; invalid policy starts no timers', async t => {
  const f = fixture(t, { enabled: false });
  assert.equal(AUTOSAVE_CONTRACT, 'foundation-browser-autosave/v1');
  assert.equal(f.clock.timers.size, 0);
  for (const patch of [{ enabled: 'yes' }, { intervalMilliseconds: 0 }, { intervalMilliseconds: Infinity },
    { operationTimeoutMilliseconds: -1 }, { operationTimeoutMilliseconds: Number.MAX_SAFE_INTEGER + 1 }]) {
    assert.throws(() => f.controller(patch), TypeError);
    assert.equal(f.clock.timers.size, 0);
  }
  const canceled = new AbortController(); canceled.abort();
  assert.throws(() => f.controller({ signal: canceled.signal }), { name: 'AbortError' });
});

for (const Adapter of [VersionConflictAdapter, OverwriteAdapter]) {
  test(`${Adapter.name}: same controller supports manual and automatic save without consumer branches`, async t => {
    const f = fixture(t);
    const adapter = new Adapter();
    const target = f.add(adapter);
    await f.clock.advance(99);
    assert.equal(adapter.store.effects, 0);
    await f.clock.advance(1);
    assert.equal(target.state.phase, 'clean');
    assert.equal(adapter.store.effects, 1);
    adapter.value = 'second draft'; target.edited();
    assert.equal(await target.save(), 'observed');
    assert.equal(adapter.store.rows.get('target-a').value, 'second draft');
    assert.equal(adapter.store.effects, 2);
    assert.equal(await target.save(), 'clean');
    if (Adapter === OverwriteAdapter) {
      assert.equal('version' in target.state.lastConfirmation.receipt, false);
      assert.equal('expectedVersion' in JSON.parse(adapter.attempts[0].payload), false);
    }
  });

  test(`${Adapter.name}: response loss retains original key/payload and identical replay has one model effect`, async t => {
    const f = fixture(t);
    const adapter = new Adapter(); adapter.loseResponse = true;
    const target = f.add(adapter);
    await target.save();
    const attempt = target.state.pending;
    assert(Object.isFrozen(attempt));
    assert.equal(target.state.phase, 'unknown');
    adapter.value = 'newer unsent input'; target.edited();
    assert.equal(await target.save(), 'blocked');
    await f.clock.advance(100);
    assert.equal(adapter.attempts.length, 1);
    await target.reconcile('replay');
    assert.equal(adapter.attempts[1], attempt);
    assert.equal(adapter.store.effects, 1);
    assert.equal(adapter.store.rows.get('target-a').value, 'first draft');
    assert.equal(target.state.phase, 'dirty');
    assert.equal(target.state.confirmedEditRevision, 1);
  });
}

test('typing does not debounce the first interval, and clean ticks create no new confirmation', async t => {
  const f = fixture(t);
  const adapter = new OverwriteAdapter(); const target = f.add(adapter);
  for (let i = 0; i < 9; i++) { await f.clock.advance(10); target.edited(); }
  await f.clock.advance(10);
  assert.equal(adapter.store.effects, 1);
  assert.equal(target.state.confirmedEditRevision, 10);
  const confirmation = target.state.lastConfirmation;
  await f.clock.advance(500);
  assert.equal(adapter.store.effects, 1);
  assert.equal(target.state.lastConfirmation, confirmation);
});

test('disabled autosave preserves manual saving, and sequential awaited manual saves release the shared slot', async t => {
  const f = fixture(t, { enabled: false });
  const a = new OverwriteAdapter(); const b = new OverwriteAdapter();
  const first = f.add(a); const second = f.add(b, 'target-b');
  await f.clock.advance(1000);
  assert.equal(a.store.effects + b.store.effects, 0);
  assert.equal(await first.save(), 'observed');
  assert.equal(await second.save(), 'observed');
  assert.equal(a.store.effects + b.store.effects, 2);
});

test('one finite pass saves metadata then its dependent frame, and defers targets registered during that pass', async t => {
  const f = fixture(t);
  const metadata = new OverwriteAdapter(); const frame = new OverwriteAdapter(); const added = new OverwriteAdapter();
  let parentConfirmed = false;
  let late;
  metadata.confirmed = () => { parentConfirmed = true; late = f.add(added, 'late-target'); };
  frame.capture = identity => parentConfirmed
    ? { kind: 'prepared', payload: JSON.stringify(identity) } : { kind: 'blocked' };
  f.add(metadata, 'metadata'); const child = f.add(frame, 'frame');
  await f.clock.advance(100);
  assert.equal(child.state.phase, 'clean');
  assert.equal(metadata.store.effects, 1); assert.equal(frame.store.effects, 1);
  assert.equal(late.state.phase, 'dirty'); assert.equal(added.store.effects, 0);
  await f.clock.advance(100);
  assert.equal(added.store.effects, 1);
});

test('unknown and conflicted targets do not block independent targets or imply project-wide success', async t => {
  const f = fixture(t);
  const a = new OverwriteAdapter(); a.write = async () => ({ kind: 'unknown' });
  const b = new VersionConflictAdapter(); b.store.rows.set('b', { version: 2, value: 'foreign newer basis' });
  const c = new OverwriteAdapter();
  const first = f.add(a); const second = f.add(b, 'b'); const third = f.add(c, 'c');
  await f.clock.advance(100);
  assert.equal(first.state.phase, 'unknown'); assert.equal(second.state.phase, 'conflict');
  assert.equal(third.state.phase, 'clean'); assert.equal(c.store.effects, 1);
});

test('edit 7 confirmation leaves edit 8 dirty and acknowledges only the submitted immutable attempt', async t => {
  const f = fixture(t);
  const gate = f.hold(); const adapter = new OverwriteAdapter();
  const write = adapter.write.bind(adapter);
  adapter.write = async (...args) => { await gate.promise; return write(...args); };
  const target = f.add(adapter, 'target-a', { editRevision: 7 });
  const saving = target.save(); await f.clock.flush();
  const original = target.state.pending;
  adapter.value = 'edit eight'; target.edited();
  assert.equal(target.unsaved(), 'unknown');
  gate.resolve(); await saving;
  assert.equal(target.state.phase, 'dirty'); assert.equal(target.state.editRevision, 8);
  assert.equal(target.state.confirmedEditRevision, 7);
  assert.equal(target.state.lastConfirmation.attempt, original);
  assert.equal(adapter.store.rows.get('target-a').value, 'first draft');
});

test('in-flight manual, tick, other-target and reentrant saves cannot dispatch twice', async t => {
  const f = fixture(t);
  const gate = f.hold(); const adapter = new OverwriteAdapter();
  adapter.write = () => gate.promise;
  const target = f.add(adapter); const other = f.add(new OverwriteAdapter(), 'other');
  const reentrant = [];
  target.subscribe(() => { reentrant.push(other.save()); });
  const saving = target.save(); await f.clock.flush();
  assert.equal(await target.save(), 'busy'); assert.equal(await other.save(), 'busy');
  await f.clock.advance(10);
  gate.resolve({ kind: 'unknown' }); await saving;
  assert.deepEqual(await Promise.all(reentrant), ['busy', 'busy']);
});

test('deadline reports unknown promptly but holds the lane until the original adapter actually settles', async t => {
  const f = fixture(t);
  const gate = f.hold(); const adapter = new OverwriteAdapter(); let signal;
  adapter.write = (_attempt, context) => { signal = context.signal; return gate.promise; };
  const target = f.add(adapter); const other = f.add(new OverwriteAdapter(), 'other');
  const saving = target.save(); await f.clock.flush();
  await f.clock.advance(30);
  assert.equal(await saving, 'observed'); assert.equal(signal.aborted, true);
  assert.equal(target.state.phase, 'unknown'); assert.equal(await other.save(), 'busy');
  gate.resolve({ kind: 'confirmed', receipt: { late: true } }); await f.clock.flush();
  assert.equal(target.state.phase, 'unknown'); assert.equal(target.state.lastConfirmation, null);
  assert.equal(await other.save(), 'observed');
});

test('expiry during preparation invokes no writer and retains dirty values', async t => {
  const f = fixture(t);
  const gate = f.hold(); const adapter = new OverwriteAdapter(); let writes = 0;
  adapter.prepare = () => gate.promise; adapter.write = async () => { writes++; return { kind: 'unknown' }; };
  const target = f.add(adapter);
  const saving = target.save(); await f.clock.flush(); await f.clock.advance(30);
  assert.equal(await saving, 'blocked'); assert.equal(target.state.phase, 'dirty');
  assert.equal(target.state.pending, null);
  gate.resolve('stored'); await f.clock.flush(); assert.equal(writes, 0);
});

test('local preparation failure does not suppress an admitted online save or restart the original budget', async t => {
  const f = fixture(t);
  const adapter = new OverwriteAdapter(); const contexts = [];
  const write = adapter.write.bind(adapter);
  adapter.prepare = async (_attempt, context) => { contexts.push(context); throw new Error('Fictional unavailable storage.'); };
  adapter.write = (attempt, context) => { contexts.push(context); return write(attempt, context); };
  const target = f.add(adapter); await target.save();
  assert.equal(target.state.phase, 'clean'); assert.equal(contexts[0], contexts[1]);
  assert.equal(contexts[0].expiresAtMonotonic, 30);
});

test('fresh clock observation rejects a late completion even before the timeout callback is delivered', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter();
  adapter.write = async () => { f.clock.time = 30; return { kind: 'confirmed', receipt: { observed: true } }; };
  const target = f.add(adapter); await target.save();
  assert.equal(target.state.phase, 'unknown'); assert.equal(target.state.lastConfirmation, null);
});

test('blocked admission never captures or writes; capture sees edits made while admission awaited', async t => {
  const f = fixture(t); const gate = f.hold(); const adapter = new OverwriteAdapter();
  adapter.admit = () => gate.promise;
  const target = f.add(adapter); const saving = target.save(); await f.clock.flush();
  target.edited(); adapter.value = 'newly admitted input'; gate.resolve('ready'); await saving;
  assert.equal(target.state.confirmedEditRevision, 2);
  assert.equal(adapter.store.rows.get('target-a').value, 'newly admitted input');
  const blocked = new OverwriteAdapter(); blocked.admit = async () => 'blocked';
  blocked.capture = () => { throw new Error('Capture must not run.'); };
  const other = f.add(blocked, 'blocked'); await other.save();
  assert.equal(other.state.phase, 'dirty'); assert.equal(other.state.fault, null);
});

test('no-effect rejection pauses automatic attempts until an edit or explicit corrected manual attempt', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter(); let requests = 0;
  const write = adapter.write.bind(adapter);
  adapter.write = async (...args) => ++requests === 1 ? { kind: 'rejected-without-effect' } : write(...args);
  const target = f.add(adapter); await target.save();
  assert.equal(target.state.phase, 'rejected'); assert.equal(target.state.pending, null);
  await f.clock.advance(200); assert.equal(requests, 1);
  target.edited(); await f.clock.advance(100);
  assert.equal(target.state.phase, 'clean'); assert.equal(requests, 2);
});

test('conflict keeps its attempt until application comparison supplies the current basis', async t => {
  const f = fixture(t); const adapter = new VersionConflictAdapter();
  adapter.store.rows.set('target-a', { value: 'newer server value', version: 4 });
  const target = f.add(adapter); await target.save(); const conflicted = target.state.pending;
  target.edited(); await f.clock.advance(100);
  assert.equal(target.state.phase, 'conflict'); assert.equal(await target.save(), 'blocked');
  adapter.baseVersion = 4; target.resumeAfterConflict(); await target.save();
  assert.equal(adapter.baseVersion, 5);
  assert.notEqual(target.state.lastConfirmation.attempt.operationKey, conflicted.operationKey);
});

for (const result of [null, {}, { kind: 'confirmed' }, { kind: 'confirmed', receipt: null }, { kind: 'failed' }]) {
  test(`malformed write outcome ${JSON.stringify(result)} retains uncertainty`, async t => {
    const f = fixture(t); const adapter = new OverwriteAdapter(); adapter.write = async () => result;
    const target = f.add(adapter); await target.save();
    assert.equal(target.state.phase, 'unknown'); assert(target.state.pending);
  });
}

test('status absence, failed lookup, unsafe status classification and missing replay never create new action identities', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter();
  const responses = [{ kind: 'unknown' }, { kind: 'rejected-without-effect' }, new Error('Fictional forbidden lookup.')];
  adapter.write = async () => ({ kind: 'unknown' });
  adapter.lookup = async () => { const result = responses.shift(); if (result instanceof Error) throw result; return result; };
  adapter.replay = undefined;
  const target = f.add(adapter); await target.save(); const attempt = target.state.pending;
  for (let i = 0; i < 3; i++) {
    await target.reconcile('status'); assert.equal(target.state.phase, 'unknown'); assert.equal(target.state.pending, attempt);
  }
  assert.equal(await target.reconcile('replay'), 'blocked'); assert.equal(target.state.pending, attempt);
});

test('confirmed lookup reconciles a lost response without dispatching another model effect', async t => {
  const f = fixture(t); const adapter = new VersionConflictAdapter(); adapter.loseResponse = true;
  const target = f.add(adapter); await target.save(); await target.reconcile('status');
  assert.equal(target.state.phase, 'clean'); assert.equal(adapter.baseVersion, 1); assert.equal(adapter.store.effects, 1);
});

test('restoration retains the old action and generation, starts unknown, and leaves the newer restored edit dirty', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter(); adapter.loseResponse = true;
  const original = f.add(adapter); await original.save(); const attempt = original.state.pending;
  await original.unregister();
  const restored = f.add(adapter, 'target-a', { editRevision: 2, generation: 'new-restored-generation', pending: attempt });
  assert.equal(restored.state.phase, 'unknown'); assert.equal(restored.state.pending.payload, attempt.payload);
  assert.equal(adapter.store.effects, 1); await restored.reconcile('status');
  assert.equal(restored.state.phase, 'dirty'); assert.equal(restored.state.confirmedEditRevision, 1);
});

test('registration rejects duplicate targets, invalid restored relationships, malformed adapters and oversized payloads', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter(); const target = f.add(adapter);
  assert.throws(() => f.add(adapter), TypeError);
  for (const initial of [{ confirmedEditRevision: 2 }, { editRevision: -1 }, { generation: '' },
    { pending: { target: 'foreign', operationKey: 'old', editRevision: 1, generation: 'old', payload: '{}' } }]) {
    assert.throws(() => f.add(adapter, 'invalid', initial), TypeError);
  }
  assert.throws(() => f.add({ admit: async () => 'ready' }, 'invalid-adapter'), TypeError);
  const large = new OverwriteAdapter(); large.capture = () => ({ kind: 'prepared', payload: 'x'.repeat(4097) });
  const denied = f.add(large, 'large'); await denied.save();
  assert.equal(denied.state.fault, 'adapter'); assert.equal(denied.state.pending, null); assert.equal(large.store.effects, 0);
  assert(Object.isFrozen(target.state));
});

test('confirmation callback failure retains established commit, pauses only its adapter and never licenses replay', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter();
  adapter.confirmed = () => { throw new Error('Fictional application baseline failure.'); };
  const target = f.add(adapter); await target.save();
  assert.equal(target.state.phase, 'clean'); assert.equal(target.state.fault, 'adapter');
  assert(target.state.lastConfirmation); target.edited(); assert.equal(await target.save(), 'blocked');
  assert.equal(await target.reconcile('replay'), 'blocked'); assert.equal(adapter.store.effects, 1);
  assert.equal(await f.add(new OverwriteAdapter(), 'other').save(), 'observed');
});

test('async confirmation is a fault and stays in the active drain until its actual promise settles', async t => {
  const f = fixture(t); const gate = f.hold(); const adapter = new OverwriteAdapter();
  adapter.confirmed = () => gate.promise;
  const target = f.add(adapter); await target.save();
  assert.equal(target.state.fault, 'adapter'); assert.equal(target.state.phase, 'clean');
  assert.equal(await f.add(new OverwriteAdapter(), 'other').save(), 'busy');
  gate.reject(new Error('Fictional async callback failure.')); await f.clock.flush();
  assert.equal(target.state.phase, 'clean');
});

test('subscriber errors and thenables are isolated; nested edits preserve the current observed revision', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter(); const target = f.add(adapter);
  let calls = 0;
  target.subscribe(() => { calls++; throw new Error('Fictional observer failure.'); });
  target.subscribe(() => Promise.reject(new Error('Fictional invalid async observer.')));
  target.subscribe(state => { if (state.phase === 'clean') target.edited(); });
  await target.save(); await f.clock.flush();
  assert.equal(calls, 1); assert.equal(target.state.phase, 'dirty'); assert.equal(target.state.fault, null);
  assert.equal(target.state.confirmedEditRevision, 1); assert.equal(target.state.editRevision, 2);
});

test('unregister during a pending callback cannot publish into a replacement target with the same key', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter(); const target = f.add(adapter);
  let replacement;
  target.subscribe(state => {
    if (state.phase === 'pending') { target.unregister(); replacement = f.add(new OverwriteAdapter()); }
  });
  await target.save(); await f.clock.flush();
  assert.equal(adapter.store.effects, 0); assert.equal(replacement.state.phase, 'dirty');
  assert.equal(replacement.state.lastConfirmation, null);
});

test('suspension aborts before dispatch, preserves uncertainty after dispatch, and resumes without timer bursts', async t => {
  const f = fixture(t); const gate = f.hold(); const adapter = new OverwriteAdapter();
  adapter.write = () => gate.promise;
  const target = f.add(adapter); const saving = target.save(); await f.clock.flush();
  f.main.suspend(); assert.equal(await saving, 'observed'); assert.equal(target.state.phase, 'unknown');
  await f.clock.advance(10000); assert.equal(f.clock.timers.size, 0);
  gate.resolve({ kind: 'confirmed', receipt: { stale: true } }); await f.clock.flush();
  const otherAdapter = new OverwriteAdapter(); const other = f.add(otherAdapter, 'other');
  f.main.resume(); await f.clock.advance(0);
  assert.equal(other.state.phase, 'clean'); assert.equal(otherAdapter.store.effects, 1);
  assert.equal(target.state.phase, 'unknown'); assert.equal(f.clock.timers.size, 1);
});

test('parent abort and disposal fence callbacks immediately; idempotent disposal waits for actual adapter settlement', async t => {
  const abort = new AbortController(); const f = fixture(t, { signal: abort.signal });
  const gate = f.hold(); const adapter = new OverwriteAdapter(); adapter.write = () => gate.promise;
  const target = f.add(adapter); const saving = target.save(); await f.clock.flush();
  abort.abort(); await saving;
  const drained = f.main.dispose(); assert.equal(drained, f.main.dispose());
  let stopped = false; drained.then(() => { stopped = true; }); await f.clock.flush(); assert.equal(stopped, false);
  assert.equal(await target.save(), 'disposed'); assert.throws(() => target.edited(), TypeError);
  gate.resolve({ kind: 'confirmed', receipt: { oldAccount: true } }); await drained;
  assert.equal(target.state.lastConfirmation, null); assert.equal(stopped, true);
});

test('timer chunking preserves intervals beyond the native signed timer range', async t => {
  const delay = 2 ** 31 + 10; const f = fixture(t, { intervalMilliseconds: delay });
  const adapter = new OverwriteAdapter(); f.add(adapter);
  await f.clock.advance(2 ** 31 - 1); assert.equal(adapter.store.effects, 0);
  await f.clock.advance(11); assert.equal(adapter.store.effects, 1);
});

test('local revision exhaustion rejects before replacing the current edit generation', async t => {
  const f = fixture(t); const target = f.add(new OverwriteAdapter(), 'target-a', { editRevision: Number.MAX_SAFE_INTEGER });
  const before = target.state; assert.throws(() => target.edited(), TypeError); assert.equal(target.state, before);
});

test('manual save does not postpone the original automatic due time', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter(); const target = f.add(adapter);
  await f.clock.advance(50); await target.save(); target.edited();
  await f.clock.advance(49); assert.equal(adapter.store.effects, 1);
  await f.clock.advance(1); assert.equal(adapter.store.effects, 2);
});

test('a slow automatic pass reschedules after settlement and retains deliberate target order', async t => {
  const f = fixture(t); const gate = f.hold(); const order = [];
  const a = new OverwriteAdapter(); const b = new OverwriteAdapter();
  const writeA = a.write.bind(a); const writeB = b.write.bind(b);
  let first = true;
  a.write = async (...args) => { order.push('a'); if (first) { first = false; await gate.promise; } return writeA(...args); };
  b.write = (...args) => { order.push('b'); return writeB(...args); };
  const targetA = f.add(a, 'a'); const targetB = f.add(b, 'b');
  await f.clock.advance(100); await f.clock.advance(20); gate.resolve(); await f.clock.flush();
  assert.deepEqual(order, ['a', 'b']); targetA.edited(); targetB.edited();
  await f.clock.advance(99); assert.deepEqual(order, ['a', 'b']);
  await f.clock.advance(1); assert.deepEqual(order, ['a', 'b', 'a', 'b']);
});

test('suspension while admission awaits cannot invoke the writer, even after the editor resumes', async t => {
  const f = fixture(t); const gate = f.hold(); const adapter = new OverwriteAdapter();
  adapter.admit = () => gate.promise;
  const target = f.add(adapter); const saving = target.save(); await f.clock.flush();
  f.main.suspend(); assert.equal(await saving, 'blocked'); f.main.resume();
  gate.resolve('ready'); await f.clock.flush();
  assert.equal(adapter.store.effects, 0); assert.equal(target.state.phase, 'dirty'); assert.equal(target.state.pending, null);
});

test('invalid async capture is never dispatched and retains its actual pending callback in drain', async t => {
  const f = fixture(t); const gate = f.hold(); const adapter = new OverwriteAdapter();
  adapter.capture = () => gate.promise;
  const target = f.add(adapter); assert.equal(await target.save(), 'blocked');
  assert.equal(target.state.fault, 'adapter'); assert.equal(adapter.store.effects, 0);
  const stopped = f.main.dispose(); let drained = false; stopped.then(() => { drained = true; });
  await f.clock.flush(); assert.equal(drained, false); gate.resolve({ kind: 'prepared', payload: '{}' }); await stopped;
  assert.equal(adapter.store.effects, 0);
});

test('UTF-8 byte admission rejects a multibyte payload that fits a misleading character count', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter();
  adapter.capture = () => ({ kind: 'prepared', payload: '😀'.repeat(1025) });
  const target = f.add(adapter); await target.save();
  assert.equal(adapter.store.effects, 0); assert.equal(target.state.fault, 'adapter');
});

for (const Adapter of [VersionConflictAdapter, OverwriteAdapter]) {
  test(`${Adapter.name}: changed bytes under an existing key cannot overwrite its recorded effect`, async t => {
    const f = fixture(t); const adapter = new Adapter(); const target = f.add(adapter); await target.save();
    const attempt = target.state.lastConfirmation.attempt;
    const modified = Object.freeze({ ...attempt, payload: JSON.stringify({ ...JSON.parse(attempt.payload), value: 'substituted' }) });
    const result = await adapter.write(modified, { signal: new AbortController().signal });
    assert.equal(result.kind, 'conflict'); assert.equal(adapter.store.effects, 1);
    assert.equal(adapter.store.rows.get('target-a').value, 'first draft');
  });
}

test('review regression: every pass preserves deliberate parent-before-child order', async t => {
  const f = fixture(t); const parent = new VersionConflictAdapter(); const child = new OverwriteAdapter();
  let requiredParentVersion = 1;
  const capture = child.capture.bind(child);
  child.capture = identity => parent.baseVersion >= requiredParentVersion ? capture(identity) : { kind: 'blocked' };
  const metadata = f.add(parent, 'metadata'); const frame = f.add(child, 'frame');
  await f.clock.advance(100); assert.equal(frame.state.phase, 'clean');
  requiredParentVersion = 2; metadata.edited(); frame.edited(); await f.clock.advance(100);
  assert.equal(parent.baseVersion, 2);
  assert.equal(frame.state.phase, 'clean', 'a ready child must not wait another interval after its parent saves');
  assert.equal(child.store.effects, 2);
});

test('review regression: controller announces timeout drain and actual release without another save probe', async t => {
  const f = fixture(t); const gate = f.hold(); const adapter = new OverwriteAdapter();
  adapter.write = () => gate.promise;
  const target = f.add(adapter); const activity = [];
  const unsubscribe = f.main.subscribeActivity(value => { activity.push(value); });
  assert.deepEqual(f.main.activity, { busy: false, draining: false, suspended: false, disposed: false });
  const saving = target.save(); await f.clock.flush();
  assert.deepEqual(f.main.activity, { busy: true, draining: false, suspended: false, disposed: false });
  await f.clock.advance(30); await saving;
  assert.deepEqual(f.main.activity, { busy: true, draining: true, suspended: false, disposed: false });
  gate.resolve({ kind: 'unknown' }); await f.clock.flush();
  assert.deepEqual(f.main.activity, { busy: false, draining: false, suspended: false, disposed: false });
  assert.equal(target.state.phase, 'unknown');
  assert.deepEqual(activity.map(value => [value.busy, value.draining]), [[true, false], [true, true], [false, false]]);
  assert(activity.every(Object.isFrozen)); unsubscribe();
});

test('activity observer disposal fences adapter admission before any operation callback', async t => {
  const f = fixture(t); const adapter = new OverwriteAdapter(); let admissions = 0;
  adapter.admit = async () => { admissions++; return 'ready'; };
  const target = f.add(adapter);
  f.main.subscribeActivity(activity => { if (activity.busy) f.main.dispose(); });
  assert.equal(await target.save(), 'blocked'); await f.clock.flush();
  assert.equal(admissions, 0); assert.equal(adapter.store.effects, 0);
  assert.deepEqual(f.main.activity, { busy: false, draining: false, suspended: false, disposed: true });
});

test('activity retains disposed drain until actual settlement and isolates faulty observers', async t => {
  const f = fixture(t); const gate = f.hold(); const adapter = new OverwriteAdapter();
  adapter.write = () => gate.promise;
  const target = f.add(adapter); const values = [];
  let calls = 0;
  f.main.subscribeActivity(() => { calls++; throw new Error('Fictional observer failure.'); });
  f.main.subscribeActivity(() => Promise.reject(new Error('Fictional async observer.')));
  f.main.subscribeActivity(activity => { values.push(activity); });
  const saving = target.save(); await f.clock.flush();
  const stopped = f.main.dispose(); await saving;
  assert.equal(f.main.activity.disposed, true); assert.equal(f.main.activity.busy, true); assert.equal(f.main.activity.draining, true);
  gate.resolve({ kind: 'unknown' }); await stopped; await f.clock.flush();
  assert.equal(calls, 1); assert.equal(values.at(-1).disposed, true); assert.equal(values.at(-1).busy, false);
});
