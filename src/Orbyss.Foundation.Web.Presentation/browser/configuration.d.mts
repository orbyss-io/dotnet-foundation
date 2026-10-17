import type { SignOutConfirmationMode } from './presentation.mjs';
export interface BrowserPresentationConfiguration {
  readonly contract: 'foundation-browser-presentation/v1';
  readonly surface: string;
  readonly signOutConfirmation: SignOutConfirmationMode;
  readonly modulePath: string;
  readonly bffModulePath: string | null;
  readonly antiforgeryPath: string | null;
  readonly logoutPath: string | null;
}
/** Loads only bounded same-origin public configuration; establishes no session proof. */
export function loadBrowserPresentation(path: string, options?: {
  signal?: AbortSignal;
  timeoutMilliseconds?: number;
}): Promise<BrowserPresentationConfiguration>;
