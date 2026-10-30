// Test-private replacement of browser built-ins. Production exposes no clock or scheduler extension point.
export class Clock {
  constructor() {
    this.time = 0;
    this.next = 0;
    this.timers = new Map();
    this.original = { performance: globalThis.performance, setTimeout: globalThis.setTimeout, clearTimeout: globalThis.clearTimeout };
    globalThis.performance = { now: () => this.time };
    globalThis.setTimeout = (callback, delay) => {
      const id = ++this.next;
      this.timers.set(id, { callback, due: this.time + delay });
      return id;
    };
    globalThis.clearTimeout = id => this.timers.delete(id);
  }

  async flush() { for (let step = 0; step < 50; step++) await Promise.resolve(); }

  async advance(milliseconds) {
    const end = this.time + milliseconds;
    for (let steps = 0; steps < 1000; steps++) {
      const first = [...this.timers.entries()].filter(([, timer]) => timer.due <= end)
        .sort((left, right) => left[1].due - right[1].due)[0];
      if (!first) { this.time = end; await this.flush(); return; }
      this.time = first[1].due;
      this.timers.delete(first[0]);
      first[1].callback();
      await this.flush();
    }
    throw new Error('The controller scheduled an unbounded timer burst.');
  }

  restore() { Object.assign(globalThis, this.original); }
}

export function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((success, failure) => { resolve = success; reject = failure; });
  return { promise, resolve, reject };
}
