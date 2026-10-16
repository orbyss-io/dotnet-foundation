import type { SignOutConfirmationMode, SignOutControl, ConfirmationCopy, ConfirmationRequest, ConfirmationView } from './presentation.mjs';
export interface BffSignOutOptions {
  surface: string;
  antiforgeryPath?: string;
  logoutPath?: string;
  mode?: SignOutConfirmationMode;
  copy?: ConfirmationCopy;
  confirmation?(request: ConfirmationRequest): ConfirmationView;
  signal?: AbortSignal;
}
/** Validates/registers the control without network acquisition. Native token acquisition begins only after sign-out admission. */
export function prepareBffSignOut(options: BffSignOutOptions): Promise<SignOutControl<void>>;
