import type { SignOutOptions, SignOutControl } from './presentation.mjs';
import type { SessionObservationOptions, BffSessionObserver } from './session.mjs';
/** Protocol freshness belongs to the selected adapter; local cached user data alone is insufficient. */
export function observeSpaSession(options: SessionObservationOptions): BffSessionObserver;
export interface SpaAuthenticationAdapter {
  /** Must synchronously clear in-memory native client credentials and private session projections. */
  clearLocalAuthentication(): undefined;
  readLocalSession(): 'anonymous' | 'authenticated' | 'unknown';
  /** Called directly from the admitted gesture; use the selected client's native protocol operation. */
  beginProviderLogout(): unknown | Promise<unknown>;
}
export interface SpaSignOutObservation { localSession: 'anonymous'; provider: 'dispatched' | 'unavailable'; }
export type SpaSignOutOptions = Omit<SignOutOptions<SpaSignOutObservation>, 'signOut'> & { adapter: SpaAuthenticationAdapter };
/** A dispatch observation does not confirm provider-session termination. */
export function createSpaSignOutControl(options: SpaSignOutOptions): SignOutControl<SpaSignOutObservation>;
