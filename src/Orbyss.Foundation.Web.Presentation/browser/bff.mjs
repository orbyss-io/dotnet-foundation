import { createSignOutControl } from './presentation.mjs';

/** Prepare the existing native BFF form transport before an enabled sign-out gesture. */
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
  const response = await fetch(csrfUrl, { credentials: 'same-origin', cache: 'no-store', redirect: 'error',
    headers: { Accept: 'application/json' }, signal });
  if (!response.ok || response.headers.get('content-type')?.split(';')[0] !== 'application/json') {
    throw new Error('Native BFF antiforgery preparation is unavailable.');
  }
  // Bound even a custom/misconfigured server before decoding its response.
  const reader = response.body?.getReader();
  if (!reader) throw new Error('Native BFF antiforgery response is missing.');
  let text = '';
  let length = 0;
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
  signal?.throwIfAborted();
  const form = document.createElement('form');
  form.method = 'post';
  form.action = logoutUrl;
  form.hidden = true;
  const token = document.createElement('input');
  token.type = 'hidden'; token.name = tokens.formFieldName; token.value = tokens.requestToken;
  form.append(token);
  document.body.append(form);
  let control;
  try {
    control = createSignOutControl({ surface, mode, copy, confirmation,
      signOut: () => { signal?.throwIfAborted(); HTMLFormElement.prototype.submit.call(form); } });
  } catch (error) { form.remove(); throw error; }
  const dispose = () => { control.dispose(); form.remove(); signal?.removeEventListener('abort', dispose); };
  signal?.addEventListener('abort', dispose, { once: true });
  return Object.freeze({ request: options => control.request(options), dispose });
}
