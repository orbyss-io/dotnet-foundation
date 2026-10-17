import { createSignOutControl } from './presentation.mjs';
import { observeSessionBoundary } from './session.mjs';

/** Bind a fresh selected SPA protocol reader to the common private DOM lifecycle. */
export function observeSpaSession(options) { return observeSessionBoundary(options); }

/** Bind a selected SPA protocol adapter without moving its fresh user gesture behind an await. */
export function createSpaSignOutControl({ adapter, ...options }) {
  if (!adapter || ['clearLocalAuthentication', 'readLocalSession', 'beginProviderLogout'].some(key => typeof adapter[key] !== 'function')) {
    throw new TypeError('A selected SPA authentication protocol adapter is required.');
  }
  return createSignOutControl({ ...options, signOut() {
    const cleared = adapter.clearLocalAuthentication();
    if (cleared !== undefined) {
      if (cleared && typeof cleared.then === 'function') Promise.resolve(cleared).catch(() => {});
      throw new TypeError('SPA local clearing must complete synchronously before provider dispatch.');
    }
    if (adapter.readLocalSession() !== 'anonymous') throw new Error('Local SPA termination was not observed; provider logout was not dispatched.');
    let dispatched;
    try { dispatched = adapter.beginProviderLogout(); }
    catch { return { localSession: 'anonymous', provider: 'unavailable' }; }
    return Promise.resolve(dispatched).then(() => ({ localSession: 'anonymous', provider: 'dispatched' }),
      () => ({ localSession: 'anonymous', provider: 'unavailable' }));
  } });
}
