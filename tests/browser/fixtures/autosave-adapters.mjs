// Fictional in-memory application adapters. These demonstrate protocol integration, not database durability.
import assert from 'node:assert/strict';

export function recordingStore() {
  return { rows: new Map(), receipts: new Map(), effects: 0 };
}

function readReceipt(store, attempt) {
  const recorded = store.receipts.get(attempt.target + '/' + attempt.operationKey);
  if (!recorded || recorded.payload !== attempt.payload) return null;
  const receipt = recorded.receipt;
  assert.equal(receipt.operationKey, attempt.operationKey);
  assert.equal(receipt.target, attempt.target);
  assert.equal(receipt.editRevision, attempt.editRevision);
  assert.equal(receipt.generation, attempt.generation);
  return receipt;
}

function record(store, attempt, extra = {}) {
  const receipt = Object.freeze({ operationKey: attempt.operationKey, target: attempt.target,
    editRevision: attempt.editRevision, generation: attempt.generation, ...extra });
  store.receipts.set(attempt.target + '/' + attempt.operationKey, { payload: attempt.payload, receipt });
  store.effects++;
  return receipt;
}

/** Application policy: compare the captured server version and require explicit review after conflict. */
export class VersionConflictAdapter {
  constructor(store = recordingStore()) {
    this.store = store;
    this.value = 'first draft';
    this.baseVersion = 0;
    this.attempts = [];
    this.loseResponse = false;
  }

  async admit(context) {
    context.signal.throwIfAborted();
    return 'ready';
  }

  capture(identity) {
    return { kind: 'prepared', payload: JSON.stringify({ ...identity, expectedVersion: this.baseVersion, value: this.value }) };
  }

  async write(attempt, context) {
    context.signal.throwIfAborted();
    this.attempts.push(attempt);
    const existing = this.store.receipts.get(attempt.target + '/' + attempt.operationKey);
    if (existing && existing.payload !== attempt.payload) return { kind: 'conflict' };
    const prior = readReceipt(this.store, attempt);
    if (prior) return { kind: 'confirmed', receipt: prior };
    const request = JSON.parse(attempt.payload);
    const current = this.store.rows.get(attempt.target);
    if ((current?.version ?? 0) !== request.expectedVersion) return { kind: 'conflict' };
    const version = request.expectedVersion + 1;
    this.store.rows.set(attempt.target, { value: request.value, version });
    const receipt = record(this.store, attempt, { version });
    if (this.loseResponse) { this.loseResponse = false; throw new Error('Fictional response loss after recording the effect.'); }
    return { kind: 'confirmed', receipt };
  }

  async lookup(attempt, context) {
    context.signal.throwIfAborted();
    const receipt = readReceipt(this.store, attempt);
    return receipt ? { kind: 'confirmed', receipt } : { kind: 'unknown' };
  }

  replay(attempt, context) { return this.write(attempt, context); }

  confirmed(attempt, receipt) {
    assert.equal(receipt, readReceipt(this.store, attempt));
    this.baseVersion = receipt.version;
  }
}

/** Application policy: explicit writes replace current content; requests and receipts carry no server version. */
export class OverwriteAdapter {
  constructor(store = recordingStore()) {
    this.store = store;
    this.value = 'first draft';
    this.attempts = [];
    this.loseResponse = false;
  }

  async admit(context) {
    context.signal.throwIfAborted();
    return 'ready';
  }

  capture(identity) {
    return { kind: 'prepared', payload: JSON.stringify({ ...identity, value: this.value }) };
  }

  async write(attempt, context) {
    context.signal.throwIfAborted();
    this.attempts.push(attempt);
    const existing = this.store.receipts.get(attempt.target + '/' + attempt.operationKey);
    if (existing && existing.payload !== attempt.payload) return { kind: 'conflict' };
    const prior = readReceipt(this.store, attempt);
    if (prior) return { kind: 'confirmed', receipt: prior };
    const request = JSON.parse(attempt.payload);
    this.store.rows.set(attempt.target, { value: request.value });
    const receipt = record(this.store, attempt);
    if (this.loseResponse) { this.loseResponse = false; throw new Error('Fictional response loss after recording the effect.'); }
    return { kind: 'confirmed', receipt };
  }

  async lookup(attempt, context) {
    context.signal.throwIfAborted();
    const receipt = readReceipt(this.store, attempt);
    return receipt ? { kind: 'confirmed', receipt } : { kind: 'unknown' };
  }

  replay(attempt, context) { return this.write(attempt, context); }

  confirmed(attempt, receipt) { assert.equal(receipt, readReceipt(this.store, attempt)); }
}
