/** Optional local save coordination. Adapters own authorization, wire admission and durable effects. */
export const AUTOSAVE_CONTRACT = 'foundation-browser-autosave/v1';

const maximumTimerDelay = 2 ** 31 - 1;
const encoder = new TextEncoder();

function requireValue(condition, message) {
  if (!condition) throw new TypeError(message);
}

function positiveInteger(value) {
  return Number.isSafeInteger(value) && value > 0;
}

function identity(value) {
  return typeof value === 'string' && value.length > 0;
}

function deferred() {
  let resolve;
  const promise = new Promise(finish => { resolve = finish; });
  return { promise, resolve };
}

function thenable(value) {
  return value !== null && (typeof value === 'object' || typeof value === 'function')
    && typeof value.then === 'function';
}

function observe(value) {
  return Promise.resolve(value).catch(() => undefined);
}

function freezeAttempt(value, target, maximumPayloadBytes, maximumRevision) {
  const fields = ['target', 'operationKey', 'editRevision', 'generation', 'payload'];
  requireValue(value && Object.keys(value).length === fields.length
    && fields.every(field => Object.hasOwn(value, field)) && value.target === target
    && identity(value.operationKey) && identity(value.generation)
    && Number.isSafeInteger(value.editRevision) && value.editRevision >= 0 && value.editRevision <= maximumRevision
    && typeof value.payload === 'string' && encoder.encode(value.payload).byteLength <= maximumPayloadBytes,
  'Declare an admitted bounded attempt for this exact target and local revision.');
  return Object.freeze({ target, operationKey: value.operationKey, editRevision: value.editRevision,
    generation: value.generation, payload: value.payload });
}

function resultKind(result, statusOnly) {
  if (!result || typeof result !== 'object') return 'unknown';
  const kind = result.kind;
  if (kind === 'confirmed') {
    return Object.hasOwn(result, 'receipt') && result.receipt !== undefined && result.receipt !== null
      ? kind : 'unknown';
  }
  return !statusOnly && (kind === 'conflict' || kind === 'rejected-without-effect') ? kind : 'unknown';
}

/** Loading this module starts no work. Each controller is confined to one editor/account lifetime. */
export function createAutosaveController(options) {
  requireValue(options && typeof options.enabled === 'boolean' && positiveInteger(options.intervalMilliseconds)
    && positiveInteger(options.operationTimeoutMilliseconds), 'Declare autosave policy and finite positive millisecond budgets.');
  const selected = Object.freeze({ ...options });
  selected.signal?.throwIfAborted();
  requireValue(Number.isFinite(performance.now())
    && performance.now() + Math.max(selected.intervalMilliseconds, selected.operationTimeoutMilliseconds) <= Number.MAX_SAFE_INTEGER,
  'Autosave time must remain representable.');
  const controller = new Controller(selected);
  return controller.public;
}

/** Owns the single editor timer, active adapter slot and registered target lifetimes. */
class Controller {
  constructor(options) {
    this.options = options;
    this.targets = new Map();
    this.disposed = false;
    this.suspended = false;
    this.active = null;
    this.pass = null;
    this.activityListeners = new Set();
    this.activityNotifying = false;
    this.activity = Object.freeze({ busy: false, draining: false, suspended: false, disposed: false });
    this.timer = undefined;
    this.nextDue = performance.now() + options.intervalMilliseconds;
    this.stop = null;
    this.parentAborted = () => { this.dispose(); };
    options.signal?.addEventListener('abort', this.parentAborted, { once: true });
    const owner = this;
    this.public = Object.freeze({ register: value => this.register(value), suspend: () => this.suspend(),
      resume: () => this.resume(), dispose: () => this.dispose(),
      get activity() { return owner.activity; },
      subscribeActivity(listener) {
        requireValue(!owner.disposed && typeof listener === 'function', 'Subscribe within the controller lifetime.');
        owner.activityListeners.add(listener);
        return () => { owner.activityListeners.delete(listener); };
      } });
    this.arm();
  }

  current(target) {
    return !this.disposed && this.targets.get(target.key) === target;
  }

  publishActivity() {
    const value = { busy: this.active !== null, draining: !!this.active && (this.active.cutoff || this.active.draining),
      suspended: this.suspended, disposed: this.disposed };
    if (Object.keys(value).every(key => value[key] === this.activity[key])) return;
    this.activity = Object.freeze(value);
    if (this.activityNotifying) return;
    this.activityNotifying = true;
    try {
      for (const listener of [...this.activityListeners]) {
        if (!this.activityListeners.has(listener)) continue;
        try {
          const returned = listener(this.activity);
          if (returned !== undefined) {
            if (thenable(returned)) observe(returned);
            this.activityListeners.delete(listener);
          }
        } catch { this.activityListeners.delete(listener); }
      }
    } finally {
      this.activityNotifying = false;
      if (this.disposed && !this.active) this.activityListeners.clear();
    }
  }

  register(options) {
    requireValue(!this.disposed && options && identity(options.target) && !this.targets.has(options.target)
      && positiveInteger(options.maximumPayloadBytes), 'Register one bounded opaque target per controller.');
    const { initial, adapter } = options;
    requireValue(initial && Number.isSafeInteger(initial.editRevision) && initial.editRevision >= 0
      && Number.isSafeInteger(initial.confirmedEditRevision) && initial.confirmedEditRevision >= 0
      && initial.confirmedEditRevision <= initial.editRevision && identity(initial.generation),
    'Declare admitted initial local save revisions.');
    requireValue(adapter && ['admit', 'capture', 'write', 'confirmed'].every(name => typeof adapter[name] === 'function')
      && ['prepare', 'lookup', 'replay'].every(name => adapter[name] === undefined || typeof adapter[name] === 'function'),
    'Declare the selected save adapter capabilities.');
    const pending = initial.pending === null ? null
      : freezeAttempt(initial.pending, options.target, options.maximumPayloadBytes, initial.editRevision);
    const methods = Object.fromEntries(['admit', 'capture', 'write', 'confirmed', 'prepare', 'lookup', 'replay']
      .filter(name => adapter[name] !== undefined).map(name => [name, adapter[name].bind(adapter)]));
    const target = { key: options.target, maximumPayloadBytes: options.maximumPayloadBytes,
      adapter: Object.freeze(methods), listeners: new Set(), notifying: false, suppress: false, retirement: null,
      state: Object.freeze({ phase: pending ? 'unknown' : initial.editRevision === initial.confirmedEditRevision ? 'clean' : 'dirty',
        editRevision: initial.editRevision, confirmedEditRevision: initial.confirmedEditRevision,
        generation: initial.generation, pending, lastConfirmation: null, fault: null }) };
    const owner = this;
    const handle = Object.freeze({
      get state() { return target.state; },
      edited() { owner.edited(target); },
      save() { return owner.request(target, 'manual'); },
      reconcile(mode) {
        requireValue(mode === 'status' || mode === 'replay', 'Select status observation or identical replay.');
        return owner.request(target, mode);
      },
      resumeAfterConflict() {
        requireValue(owner.current(target) && target.state.phase === 'conflict' && !target.state.fault,
          'Resolve the current target conflict before resuming it.');
        owner.update(target, { phase: 'dirty', pending: null });
      },
      unsaved() {
        const state = target.state;
        return state.phase === 'pending' || state.phase === 'unknown' ? 'unknown'
          : state.editRevision !== state.confirmedEditRevision || state.phase === 'conflict' ? 'dirty' : 'clean';
      },
      subscribe(listener) {
        requireValue(owner.current(target) && typeof listener === 'function', 'Subscribe within the target lifetime.');
        target.listeners.add(listener);
        return () => { target.listeners.delete(listener); };
      },
      unregister() { return owner.unregister(target); },
    });
    this.targets.set(target.key, target);
    return handle;
  }

  update(target, changes) {
    target.state = Object.freeze({ ...target.state, ...changes });
    this.publish(target);
  }

  publish(target) {
    if (!this.current(target) || target.suppress || target.notifying) return;
    target.notifying = true;
    try {
      for (const listener of [...target.listeners]) {
        if (!this.current(target)) break;
        if (!target.listeners.has(listener)) continue;
        try {
          const returned = listener(target.state);
          if (returned !== undefined) {
            if (thenable(returned)) observe(returned);
            target.listeners.delete(listener);
          }
        } catch { target.listeners.delete(listener); }
      }
    } finally { target.notifying = false; }
  }

  edited(target) {
    requireValue(this.current(target) && target.state.editRevision < Number.MAX_SAFE_INTEGER,
      'Edit only a live target with a representable local revision.');
    const generation = crypto.randomUUID();
    this.update(target, { editRevision: target.state.editRevision + 1, generation,
      phase: target.state.phase === 'clean' || target.state.phase === 'rejected' ? 'dirty' : target.state.phase });
  }

  arm() {
    clearTimeout(this.timer);
    this.timer = undefined;
    if (!this.options.enabled || this.disposed || this.suspended || this.active || this.pass) return;
    const remaining = this.nextDue - performance.now();
    this.timer = setTimeout(() => {
      this.timer = undefined;
      if (performance.now() < this.nextDue) this.arm();
      else this.automatic();
    }, Math.min(maximumTimerDelay, Math.max(0, remaining)));
  }

  async automatic() {
    if (this.disposed || this.suspended || this.active || this.pass) return;
    const pass = this.pass = { stop: false };
    const targets = [...this.targets.values()];
    try {
      for (const target of targets) {
        if (pass.stop || this.disposed || this.suspended) break;
        await this.request(target, 'automatic');
        if (this.active) await this.active.drain.promise;
      }
    } finally {
      if (this.pass === pass) this.pass = null;
      this.nextDue = performance.now() + this.options.intervalMilliseconds;
      this.arm();
    }
  }

  request(target, trigger) {
    if (!this.current(target)) return Promise.resolve('disposed');
    if (this.suspended || target.state.fault) return Promise.resolve('blocked');
    if (this.active) return Promise.resolve('busy');
    if (trigger !== 'automatic' && this.pass) this.pass.stop = true;
    const reconciliation = trigger === 'status' || trigger === 'replay';
    if (reconciliation) {
      if (target.state.phase !== 'unknown' || !target.state.pending
        || typeof target.adapter[trigger === 'status' ? 'lookup' : 'replay'] !== 'function') return Promise.resolve('blocked');
    } else {
      if (target.state.phase === 'clean') return Promise.resolve('clean');
      if (target.state.phase !== 'dirty' && !(trigger === 'manual' && target.state.phase === 'rejected')) return Promise.resolve('blocked');
    }
    const operation = { target, trigger, reconciliation, adapter: target.adapter,
      abort: new AbortController(), expires: performance.now() + this.options.operationTimeoutMilliseconds,
      completion: deferred(), drain: deferred(), cutoff: false, enteredWrite: false, accepted: false,
      attempt: reconciliation ? target.state.pending : null, deadlineTimer: undefined, result: 'blocked', draining: false };
    this.active = operation;
    clearTimeout(this.timer);
    this.timer = undefined;
    this.publishActivity();
    if (!operation.cutoff) this.armDeadline(operation);
    this.execute(operation).catch(() => {
      this.cutoff(operation);
    }).finally(() => {
      clearTimeout(operation.deadlineTimer);
      if (this.active === operation) this.active = null;
      this.publishActivity();
      operation.drain.resolve();
      operation.completion.resolve(operation.result);
      if (!this.current(target)) target.adapter = null;
      this.arm();
    });
    return operation.completion.promise;
  }

  armDeadline(operation) {
    operation.deadlineTimer = setTimeout(() => {
      if (performance.now() < operation.expires) this.armDeadline(operation);
      else this.cutoff(operation);
    }, Math.min(maximumTimerDelay, Math.max(0, operation.expires - performance.now())));
  }

  cutoff(operation) {
    if (operation.cutoff) return;
    operation.cutoff = true;
    clearTimeout(operation.deadlineTimer);
    if (!operation.accepted && operation.attempt) {
      this.update(operation.target, operation.enteredWrite || operation.reconciliation
        ? { phase: 'unknown' } : { phase: 'dirty', pending: null });
    }
    operation.abort.abort();
    this.publishActivity();
    operation.completion.resolve(operation.enteredWrite || operation.reconciliation ? 'observed' : 'blocked');
  }

  fresh(operation) {
    if (!this.current(operation.target) || this.suspended || operation.abort.signal.aborted
      || performance.now() >= operation.expires) this.cutoff(operation);
    return !operation.cutoff;
  }

  async execute(operation) {
    const { target, adapter, trigger } = operation;
    const context = Object.freeze({ signal: operation.abort.signal, expiresAtMonotonic: operation.expires, trigger });
    try {
      if (!this.fresh(operation)) return;
      const admission = await adapter.admit(context);
      if (!this.fresh(operation)) return;
      if (admission !== 'ready') {
        if (admission !== 'blocked') this.update(target, { fault: 'adapter' });
        return;
      }
      if (!operation.reconciliation) {
        // Admission may await user/session work while input changes. Capture the current edit only now.
        const identity = Object.freeze({ target: target.key, operationKey: crypto.randomUUID(),
          editRevision: target.state.editRevision, generation: target.state.generation });
        let captured;
        try { captured = adapter.capture(identity); }
        catch { this.update(target, { fault: 'adapter' }); return; }
        if (thenable(captured)) {
          this.update(target, { fault: 'adapter' });
          operation.draining = true;
          this.publishActivity();
          operation.completion.resolve('blocked');
          await observe(captured);
          return;
        }
        if (!this.fresh(operation)) return;
        if (captured?.kind === 'blocked') return;
        if (captured?.kind === 'invalid') { this.update(target, { phase: 'rejected' }); return; }
        try {
          requireValue(captured?.kind === 'prepared', 'Capture must return a synchronous prepared payload.');
          operation.attempt = freezeAttempt({ ...identity, payload: captured.payload }, target.key,
            target.maximumPayloadBytes, target.state.editRevision);
        } catch { this.update(target, { fault: 'adapter' }); return; }
        this.update(target, { phase: 'pending', pending: operation.attempt });
      } else this.update(target, { phase: 'pending' });
      if (!this.fresh(operation)) return;
      if (trigger !== 'status' && adapter.prepare) {
        let prepared = 'unavailable';
        try { prepared = await adapter.prepare(operation.attempt, context); }
        catch { /* The recovery owner reports unavailable storage independently of the online result. */ }
        if (!this.fresh(operation)) return;
        if (prepared !== 'stored' && prepared !== 'unavailable') {
          this.update(target, { fault: 'adapter', phase: operation.reconciliation ? 'unknown' : 'dirty',
            pending: operation.reconciliation ? operation.attempt : null });
          return;
        }
      }
      if (!this.fresh(operation)) return;
      const method = trigger === 'status' ? 'lookup' : trigger === 'replay' ? 'replay' : 'write';
      operation.enteredWrite = method !== 'lookup';
      const result = await adapter[method](operation.attempt, context);
      if (!this.fresh(operation)) return;
      const kind = resultKind(result, trigger === 'status');
      if (kind === 'confirmed') await this.confirm(operation, result.receipt);
      else this.update(target, kind === 'rejected-without-effect' ? { phase: 'rejected', pending: null }
        : { phase: kind === 'conflict' ? 'conflict' : 'unknown' });
      operation.result = 'observed';
    } catch {
      if (!operation.accepted && this.fresh(operation) && (operation.enteredWrite || operation.reconciliation)) {
        this.update(target, { phase: 'unknown' });
        operation.result = 'observed';
      }
    }
  }

  async confirm(operation, receipt) {
    const { target, attempt, adapter } = operation;
    operation.accepted = true;
    clearTimeout(operation.deadlineTimer);
    target.suppress = true;
    let returned;
    try {
      this.update(target, { confirmedEditRevision: attempt.editRevision, pending: null,
        phase: target.state.editRevision === attempt.editRevision ? 'clean' : 'dirty',
        lastConfirmation: Object.freeze({ attempt, receipt }) });
      returned = adapter.confirmed(attempt, receipt);
      if (returned !== undefined) this.update(target, { fault: 'adapter' });
    } catch { this.update(target, { fault: 'adapter' }); }
    finally { target.suppress = false; this.publish(target); }
    operation.result = 'observed';
    if (returned !== undefined) {
      operation.draining = true;
      this.publishActivity();
      operation.completion.resolve('observed');
      await observe(returned);
    }
  }

  suspend() {
    if (this.disposed || this.suspended) return;
    this.suspended = true;
    clearTimeout(this.timer);
    this.timer = undefined;
    if (this.pass) this.pass.stop = true;
    if (this.active) this.cutoff(this.active);
    this.publishActivity();
  }

  resume() {
    if (this.disposed || !this.suspended) return;
    this.suspended = false;
    this.publishActivity();
    this.arm();
  }

  unregister(target) {
    if (target.retirement) return target.retirement;
    if (this.targets.get(target.key) === target) this.targets.delete(target.key);
    target.listeners.clear();
    if (this.active?.target === target) {
      this.cutoff(this.active);
      target.retirement = this.active.drain.promise;
    } else { target.adapter = null; target.retirement = Promise.resolve(); }
    return target.retirement;
  }

  dispose() {
    if (this.stop) return this.stop;
    this.disposed = true;
    clearTimeout(this.timer);
    this.timer = undefined;
    this.options.signal?.removeEventListener('abort', this.parentAborted);
    if (this.pass) this.pass.stop = true;
    this.stop = this.active?.drain.promise ?? Promise.resolve();
    for (const target of this.targets.values()) {
      target.listeners.clear();
      if (this.active?.target !== target) target.adapter = null;
    }
    this.targets.clear();
    if (this.active) this.cutoff(this.active);
    this.publishActivity();
    return this.stop;
  }
}
