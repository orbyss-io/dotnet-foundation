import { createSignOutControl, unsavedWork, type UnsavedWorkState }
  from '../../src/Orbyss.Foundation.Web.Presentation/browser/presentation.mjs';
import { prepareBffSignOut } from '../../src/Orbyss.Foundation.Web.Presentation/browser/bff.mjs';
import { createSpaSignOutControl } from '../../src/Orbyss.Foundation.Web.Presentation/browser/spa.mjs';
import { observeBffSession } from '../../src/Orbyss.Foundation.Web.Presentation/browser/session.mjs';

let state: UnsavedWorkState = 'unknown';
const registry = unsavedWork('typed-application');
const unregister = registry.register({ getState: () => state });
const control = createSignOutControl({ surface: 'typed-application', mode: 'when-unsaved',
  signOut: () => Promise.resolve({ localTermination: 'confirmed' as const }) });
state = 'dirty';
void control.request({ returnFocus: document.createElement('button') }).then(result => {
  if (result.status === 'dispatched') {
    const confirmed: 'confirmed' = result.result.localTermination;
    void confirmed;
  } else {
    const dispatched: false = result.dispatched;
    void dispatched;
  }
});
unregister();
control.dispose();
void prepareBffSignOut({ surface: '/application', mode: 'always', signal: new AbortController().signal });
const spa = createSpaSignOutControl({ surface: '/spa', adapter: {
  clearLocalAuthentication: () => undefined, readLocalSession: () => 'anonymous', beginProviderLogout: () => undefined,
} });
spa.dispose();
const observer = observeBffSession({ privateRoots: [document.createElement('section')] });
void observer.ready; observer.dispose();
// @ts-expect-error An asynchronous clear would lose synchronous local admission and user activation.
createSpaSignOutControl({ surface: '/spa', adapter: { clearLocalAuthentication: async () => undefined, readLocalSession: () => 'anonymous', beginProviderLogout: () => undefined } });
// @ts-expect-error A pending save is unknown, never an async clean assertion.
registry.register({ getState: async () => 'clean' });
// @ts-expect-error Unknown policy names cannot configure a standard control.
createSignOutControl({ surface: 'typed-application', mode: 'when-saved', signOut: () => undefined });
