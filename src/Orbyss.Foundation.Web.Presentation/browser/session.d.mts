export type NativeSessionState = 'authenticated' | 'anonymous' | 'unavailable';
export interface NativeSessionObservation { readonly state: NativeSessionState; }
export interface SessionBoundaryOptions {
  privateRoots: HTMLElement[];
  /** Called before private roots can become visible; throwing leaves them hidden. */
  onObservation?(observation: NativeSessionObservation): undefined;
  signal?: AbortSignal;
  timeoutMilliseconds?: number;
}
export interface BffSessionObserverOptions extends SessionBoundaryOptions { userPath?: string; }
export interface SessionObservationOptions extends SessionBoundaryOptions {
  /** Fresh selected-protocol observation; errors/abort never admit private presentation. */
  observe(signal: AbortSignal): Promise<NativeSessionObservation>;
}
export interface BffSessionObserver {
  ready: Promise<NativeSessionObservation | { readonly state: 'superseded' }>;
  refresh(): Promise<NativeSessionObservation | { readonly state: 'superseded' }>;
  /** Cancels reads and removes handlers; private roots remain hidden. */
  dispose(): void;
}
/** Never writes browser storage, discards drafts, retries a domain operation or initiates logout. */
export function observeBffSession(options: BffSessionObserverOptions): BffSessionObserver;
/** Shared lifecycle only: the reader owns actual protocol/response validation. */
export function observeSessionBoundary(options: SessionObservationOptions): BffSessionObserver;
