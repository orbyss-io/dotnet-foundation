import { createSignOutControl } from './presentation.mjs';

/** Prepare a control without token acquisition, logout dispatch or private-state changes. */
export async function prepareBffSignOut({ surface, antiforgeryPath = '/bff/antiforgery',
  logoutPath = '/bff/logout', mode = 'when-unsaved', copy, confirmation, signal } = {}) {
  const local = value => {
    if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//') || /[\\\u0000-\u0020\u007f?#%]/u.test(value)) {
      throw new TypeError('BFF transport requires configured literal same-origin paths.');
    }
    const url = new URL(value, location.href);
    if (url.origin !== location.origin || url.pathname !== value) throw new TypeError('Invalid BFF transport path.');
    return url.href;
  };
  const csrfUrl = local(antiforgeryPath);
  const logoutUrl = local(logoutPath);
  signal?.throwIfAborted();
  let form;
  let acquisition;
  let finishNavigation;
  let disposed = false;
  async function signOut() {
    const controller = acquisition = new AbortController();
    // This function is first invoked directly from the admitted gesture. BFF
    // uses same-window form navigation, so it needs no delayed popup gesture.
    const response = await fetch(csrfUrl, { credentials: 'same-origin', cache: 'no-store', redirect: 'error',
      headers: { Accept: 'application/json' }, signal: controller.signal });
    if (!response.ok || response.headers.get('content-type')?.split(';')[0] !== 'application/json') {
      throw new Error('Native BFF antiforgery preparation is unavailable.');
    }
    const reader = response.body?.getReader();
    if (!reader) throw new Error('Native BFF antiforgery response is missing.');
    let text = ''; let length = 0;
    const decoder = new TextDecoder('utf-8', { fatal: true });
    try {
      while (true) {
        const item = await reader.read();
        if (item.done) break;
        length += item.value.byteLength;
        if (length > 16384) throw new Error('Native BFF antiforgery response exceeds its transport budget.');
        text += decoder.decode(item.value, { stream: true });
      }
      text += decoder.decode();
    } finally { await reader.cancel(); reader.releaseLock(); }
    const tokens = JSON.parse(text);
    if (!tokens || tokens.headerName !== 'X-CSRF-TOKEN' || tokens.formFieldName !== '__RequestVerificationToken'
      || typeof tokens.requestToken !== 'string' || !/^[A-Za-z0-9_-]{1,8192}$/u.test(tokens.requestToken)) {
      throw new Error('Native BFF antiforgery response violates the selected contract.');
    }
    controller.signal.throwIfAborted();
    if (disposed) throw new Error('Native BFF sign-out control is disposed.');
    form = document.createElement('form');
    form.method = 'post'; form.action = logoutUrl; form.hidden = true;
    const token = document.createElement('input');
    token.type = 'hidden'; token.name = tokens.formFieldName; token.value = tokens.requestToken;
    form.append(token); document.body.append(form);
    const navigation = new Promise(resolve => { finishNavigation = resolve; });
    window.addEventListener('pagehide', finishNavigation, { once: true });
    try {
      HTMLFormElement.prototype.submit.call(form);
      // Keep all controls on this surface serialized while navigation is pending.
      // A page transition is not proof of local or provider session termination.
      await navigation;
    } finally {
      window.removeEventListener('pagehide', finishNavigation); finishNavigation = undefined;
      form.remove(); form = undefined;
    }
  }
  const control = createSignOutControl({ surface, mode, copy, confirmation, signOut });
  const dispose = () => {
    if (disposed) return;
    disposed = true; acquisition?.abort(); finishNavigation?.(); control.dispose(); form?.remove();
    signal?.removeEventListener('abort', dispose);
  };
  signal?.addEventListener('abort', dispose, { once: true });
  return Object.freeze({ request: options => control.request(options), dispose });
}
