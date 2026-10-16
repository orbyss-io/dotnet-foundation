import { createSignOutControl, unsavedWork, type UnsavedWorkState }
  from '../../src/Orbyss.Foundation.Web.Presentation/browser/presentation.mjs';

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
// @ts-expect-error A pending save is unknown, never an async clean assertion.
registry.register({ getState: async () => 'clean' });
// @ts-expect-error Unknown policy names cannot configure a standard control.
createSignOutControl({ surface: 'typed-application', mode: 'when-saved', signOut: () => undefined });
