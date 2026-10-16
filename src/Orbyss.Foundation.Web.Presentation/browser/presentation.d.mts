/** Current synchronous local persistence state; pending saves are unknown. */
export type UnsavedWorkState = 'clean' | 'dirty' | 'unknown';
export type SignOutConfirmationMode = 'never' | 'when-unsaved' | 'always';
export interface UnsavedWorkSource { getState(): UnsavedWorkState; }
export interface UnsavedWorkRegistry {
  /** Dispose with the editable component; disposal is idempotent. */
  register(source: UnsavedWorkSource): () => void;
  getState(): UnsavedWorkState;
}
export interface ConfirmationCopy {
  title?: string;
  description?: string;
  cancel?: string;
  confirm?: string;
}
export interface ConfirmationView {
  focus(): void;
  /** Close without confirming, cancelling or dispatching a protocol action. */
  dismiss(): void;
}
export interface ConfirmationRequest {
  /** Invoke directly from the fresh trusted Confirm gesture, before any await. */
  onConfirm(): void;
  onCancel(): void;
  copy?: ConfirmationCopy;
  /** Invoking control, including browsers where clicking a button does not focus it. */
  returnFocus?: HTMLElement;
}
export interface SignOutOptions<T> {
  /** Stable configured application/shell identity, shared across its bundles. */
  surface: string;
  mode?: SignOutConfirmationMode;
  /** Secure native protocol action; called synchronously only after admission. */
  signOut(): T | Promise<T>;
  /** Explicit custom presentation seam; defaults to the shared native dialog. */
  confirmation?(request: ConfirmationRequest): ConfirmationView;
  copy?: ConfirmationCopy;
}
export type SignOutResult<T> = { status: 'cancelled'; dispatched: false }
  | { status: 'dispatched'; result: T };
export interface SignOutControl<T> {
  /** Call in the original user gesture; concurrent controls share one request. */
  request(options?: { returnFocus?: HTMLElement }): Promise<SignOutResult<T>>;
  dispose(): void;
}
export function unsavedWork(surface: string): UnsavedWorkRegistry;
export function showSignOutConfirmation(request: ConfirmationRequest): ConfirmationView;
export function createSignOutControl<T>(options: SignOutOptions<T>): SignOutControl<T>;
