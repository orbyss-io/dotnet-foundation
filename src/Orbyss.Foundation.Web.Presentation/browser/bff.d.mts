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
/** Performs read-only native token preparation. Enable controls only after this resolves. */
export function prepareBffSignOut(options: BffSignOutOptions): Promise<SignOutControl<void>>;
