import { UserManager, InMemoryWebStorage, WebStorageStateStore } from '/vendor/oidc-client-ts.mjs';
import { createSpaSignOutControl, observeSpaSession } from '/modules/spa.mjs';
import { unsavedWork } from '/modules/presentation.mjs';

const configuration = await (await fetch('/configuration')).json();
const memory = new InMemoryWebStorage();
const states = new InMemoryWebStorage();
const settings = { authority: configuration.authority, client_id: 'fixture-spa', response_type: 'code', scope: 'openid profile',
  redirect_uri: location.origin + '/callback', popup_redirect_uri: location.origin + '/callback',
  popup_post_logout_redirect_uri: location.origin + '/logout-callback', automaticSilentRenew: false, monitorSession: false,
  loadUserInfo: false, revokeTokensOnSignout: false, userStore: new WebStorageStateStore({ store: memory }),
  stateStore: new WebStorageStateStore({ store: states }) };
const manager = new UserManager(settings);
if (location.pathname === '/callback') { await manager.signinPopupCallback(); }
else if (location.pathname === '/logout-callback') { await manager.signoutPopupCallback(); }
else {
  const privateRoot = document.querySelector('#private');
  const status = document.querySelector('#status');
  let current = null; let hint; let dirty = false; let logoutObservation;
  const observations = []; const openings = [];
  const originalOpen = window.open.bind(window);
  window.open = (...args) => {
    openings.push({ activation: navigator.userActivation.isActive, localEmpty: memory.length === 0, projectionEmpty: current === null });
    return originalOpen(...args);
  };
  function clear() { manager.stopSilentRenew(); memory.clear(); states.clear(); current = null; }
  const observer = observeSpaSession({ privateRoots: [privateRoot], timeoutMilliseconds: 2000,
    async observe(signal) {
      const selected = await manager.getUser();
      if (!selected || selected.expired) { clear(); return { state: 'anonymous' }; }
      const response = await fetch(configuration.authority + '/userinfo', { cache: 'no-store', redirect: 'error', signal,
        headers: { Authorization: 'Bearer ' + selected.access_token } });
      if (response.status === 401) { clear(); return { state: 'anonymous' }; }
      if (!response.ok) return { state: 'unavailable' };
      const text = await response.text();
      if (text.length > 1024 || JSON.parse(text).sub !== selected.profile.sub) return { state: 'unavailable' };
      current = selected;
      return { state: 'authenticated' };
    }, onObservation(value) {
      observations.push(value.state); status.textContent = value.state + (logoutObservation ? '; provider=' + logoutObservation : '');
      if (value.state === 'authenticated' && !privateRoot.children.length) {
        const text = document.createElement('p'); text.textContent = 'Sensitive synthetic SPA content';
        const input = document.createElement('input'); input.setAttribute('aria-label', 'Raw private draft'); input.value = ' 1e- ';
        input.addEventListener('input', () => { dirty = true; }); privateRoot.append(text, input);
      }
    } });
  unsavedWork('/native-spa').register({ getState: () => dirty ? 'dirty' : 'clean' });
  const control = createSpaSignOutControl({ surface: '/native-spa', mode: 'when-unsaved', adapter: {
    clearLocalAuthentication() { hint = current?.id_token; clear(); privateRoot.hidden = true; },
    readLocalSession: () => memory.length === 0 && current === null ? 'anonymous' : 'unknown',
    beginProviderLogout() {
      const idToken = hint; hint = undefined;
      const deadline = new AbortController(); const timer = setTimeout(() => deadline.abort(), 1000);
      // The native client opens its popup synchronously before its first await.
      return manager.signoutPopup({ id_token_hint: idToken, popupSignal: deadline.signal }).finally(() => clearTimeout(timer));
    },
  } });
  document.querySelector('#sign-in').onclick = () => {
    logoutObservation = undefined;
    window.signinResult = manager.signinPopup({ nonce: crypto.randomUUID() }).then(async user => {
      current = user; await observer.refresh(); return 'authenticated';
    }, () => { clear(); status.textContent = 'unavailable'; return 'unavailable'; });
  };
  const button = document.querySelector('#sign-out');
  button.onclick = () => { window.signoutResult = control.request({ returnFocus: button }).then(value => {
    if (value.status === 'dispatched') { logoutObservation = value.result.provider; status.textContent = 'anonymous; provider=' + logoutObservation; }
    return value;
  }); };
  window.fixture = { manager, observer, observations, openings, setDirty(value) { dirty = value; },
    async state() { return { nativeUserAbsent: (await manager.getUser()) === null, memory: memory.length,
      local: localStorage.length, session: sessionStorage.length, privateVisible: !privateRoot.hidden,
      status: status.textContent, openings }; } };
  window.addEventListener('pageshow', event => { window.persistedSpaRestore = event.persisted; });
  await observer.ready;
}
