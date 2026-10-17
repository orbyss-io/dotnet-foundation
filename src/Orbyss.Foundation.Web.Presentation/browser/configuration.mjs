/** Bounded public configuration admission; no authentication or protocol dispatch. */
export async function loadBrowserPresentation(path, { signal, timeoutMilliseconds = 5000 } = {}) {
  const literal = value => typeof value === 'string' && value.startsWith('/') && !value.includes('//')
    && !/[\\\u0000-\u0020\u007f?#%]/u.test(value) && new URL(value, location.href).pathname === value;
  if (!literal(path) || !path.endsWith('/configuration') || !Number.isSafeInteger(timeoutMilliseconds)
    || timeoutMilliseconds < 1 || timeoutMilliseconds > 30000) throw new TypeError('Declare a literal native configuration route and bounded timeout.');
  signal?.throwIfAborted();
  const controller = new AbortController();
  const selectedSignal = signal ? AbortSignal.any([signal, controller.signal]) : controller.signal;
  const timer = setTimeout(() => controller.abort(), timeoutMilliseconds);
  try {
    const response = await fetch(path, { credentials: 'same-origin', cache: 'no-store', redirect: 'error',
      headers: { Accept: 'application/json' }, signal: selectedSignal });
    if (!response.ok || response.headers.get('content-type')?.split(';')[0] !== 'application/json')
      throw new Error('Native presentation configuration unavailable.');
    const reader = response.body?.getReader();
    if (!reader) throw new Error('Native presentation configuration missing.');
    const decoder = new TextDecoder('utf-8', { fatal: true });
    let text = ''; let length = 0;
    try {
      while (true) {
        const item = await reader.read();
        if (item.done) break;
        length += item.value.byteLength;
        if (length > 16384) throw new Error('Native presentation configuration exceeds its read budget.');
        text += decoder.decode(item.value, { stream: true });
      }
      text += decoder.decode();
    } finally { await reader.cancel(); reader.releaseLock(); }
    selectedSignal.throwIfAborted();
    const value = JSON.parse(text);
    const prefix = path.slice(0, -'/configuration'.length);
    const fields = ['contract', 'surface', 'signOutConfirmation', 'modulePath', 'bffModulePath', 'antiforgeryPath', 'logoutPath'];
    if (!value || typeof value !== 'object' || Array.isArray(value)
      || Object.keys(value).length !== fields.length || !fields.every(field => Object.hasOwn(value, field))
      || value.contract !== 'foundation-browser-presentation/v1' || !literal(value.surface)
      || value.surface !== '/' && (value.surface.endsWith('/') || !prefix.startsWith(value.surface + '/'))
      || !['never', 'when-unsaved', 'always'].includes(value.signOutConfirmation)
      || value.modulePath !== prefix + '/assets/v1/presentation.mjs') throw new TypeError('Native presentation configuration violates its selected contract.');
    const base = value.surface === '/' ? '' : value.surface;
    if (value.bffModulePath === null ? value.antiforgeryPath !== null || value.logoutPath !== null
      : value.bffModulePath !== prefix + '/assets/v1/bff.mjs' || value.antiforgeryPath !== base + '/bff/antiforgery'
        || value.logoutPath !== base + '/bff/logout') throw new TypeError('Native presentation transport paths differ from the selected surface.');
    return Object.freeze(value);
  } finally { clearTimeout(timer); }
}
