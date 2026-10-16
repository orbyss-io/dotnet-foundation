/** Protect private DOM across hide/show; the selected reader owns actual protocol freshness. */
export function observeSessionBoundary({ privateRoots, observe, onObservation = () => {}, signal, timeoutMilliseconds = 5000 } = {}) {
  if (!Array.isArray(privateRoots) || !privateRoots.length || privateRoots.length > 64
    || privateRoots.some(root => !(root instanceof HTMLElement)) || typeof onObservation !== 'function'
    || typeof observe !== 'function' || !Number.isSafeInteger(timeoutMilliseconds) || timeoutMilliseconds < 1 || timeoutMilliseconds > 30000) {
    throw new TypeError('Declare bounded private DOM roots and a session observation callback.');
  }
  signal?.throwIfAborted();
  const roots = [...new Set(privateRoots)].map(root => ({ root, hidden: root.hidden, inert: root.inert,
    display: root.style.getPropertyValue('display'), displayPriority: root.style.getPropertyPriority('display') }));
  let epoch = 0;
  let disposed = false;
  let pending;
  function hide() {
    epoch++;
    pending?.abort();
    for (const { root } of roots) { root.hidden = true; root.inert = true; root.style.setProperty('display', 'none', 'important'); }
  }
  async function refresh() {
    if (disposed) throw new Error('Native session observer is disposed.');
    hide();
    const current = epoch;
    const controller = pending = new AbortController();
    let state = 'unavailable';
    let rejectAbort;
    const aborted = new Promise((_, reject) => { rejectAbort = () => reject(new DOMException('Session observation cancelled or expired.', 'AbortError')); });
    controller.signal.addEventListener('abort', rejectAbort, { once: true });
    const timeout = setTimeout(() => controller.abort(), timeoutMilliseconds);
    try {
      const result = await Promise.race([observe(controller.signal), aborted]);
      if (!result || !['authenticated', 'anonymous', 'unavailable'].includes(result.state)) throw new Error('Session observation invalid.');
      state = result.state;
    } catch { /* No response, abort, malformed or oversized response cannot admit private presentation. */ }
    finally { clearTimeout(timeout); controller.signal.removeEventListener('abort', rejectAbort); }
    if (disposed || current !== epoch) return Object.freeze({ state: 'superseded' });
    const observation = Object.freeze({ state });
    // A failed application callback leaves the boundary hidden and preserves the error.
    const published = onObservation(observation);
    if (published !== undefined) {
      if (published && typeof published.then === 'function') Promise.resolve(published).catch(() => {});
      throw new TypeError('Private session observation callbacks must complete synchronously.');
    }
    if (disposed || current !== epoch) return Object.freeze({ state: 'superseded' });
    if (state === 'authenticated') {
      for (const { root, hidden, inert, display, displayPriority } of roots) {
        root.hidden = hidden; root.inert = inert;
        if (display) root.style.setProperty('display', display, displayPriority); else root.style.removeProperty('display');
      }
    }
    return observation;
  }
  const shown = () => { refresh().catch(() => window.dispatchEvent(new Event('foundation:session-observation-failed'))); };
  const visibility = () => { if (document.visibilityState === 'visible') shown(); };
  const dispose = () => {
    if (disposed) return;
    disposed = true; hide();
    window.removeEventListener('pagehide', hide); window.removeEventListener('pageshow', shown);
    document.removeEventListener('visibilitychange', visibility); signal?.removeEventListener('abort', dispose);
  };
  window.addEventListener('pagehide', hide); window.addEventListener('pageshow', shown);
  document.addEventListener('visibilitychange', visibility); signal?.addEventListener('abort', dispose, { once: true });
  return Object.freeze({ ready: refresh(), refresh, dispose });
}

/** Read the native BFF machine projection with a finite body and original abort signal. */
export function observeBffSession({ userPath = '/bff/user', ...options } = {}) {
  if (typeof userPath !== 'string' || !userPath.startsWith('/') || userPath.startsWith('//')
    || /[\\\u0000-\u0020\u007f?#%]/u.test(userPath) || new URL(userPath, location.href).pathname !== userPath) {
    throw new TypeError('Native BFF session observation requires a configured literal same-origin path.');
  }
  return observeSessionBoundary({ ...options, async observe(signal) {
    const response = await fetch(userPath, { credentials: 'same-origin', cache: 'no-store', redirect: 'error',
      headers: { Accept: 'application/json' }, signal });
    if (!response.ok || response.headers.get('content-type')?.split(';')[0] !== 'application/json') throw new Error('Native session response unavailable.');
    const reader = response.body?.getReader();
    if (!reader) throw new Error('Native session response missing.');
    const decoder = new TextDecoder('utf-8', { fatal: true });
    let text = ''; let bytes = 0;
    try {
      while (true) {
        const item = await reader.read();
        if (item.done) break;
        bytes += item.value.byteLength;
        if (bytes > 65536) throw new Error('Native session observation exceeds its bounded read budget.');
        text += decoder.decode(item.value, { stream: true });
      }
      text += decoder.decode();
    } finally { await reader.cancel(); reader.releaseLock(); }
    const result = JSON.parse(text);
    if (result?.authenticated !== true && result?.authenticated !== false) throw new Error('Native session projection invalid.');
    return { state: result.authenticated ? 'authenticated' : 'anonymous' };
  } });
}
