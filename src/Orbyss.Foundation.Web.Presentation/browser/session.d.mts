export type NativeSessionState = 'authenticated' | 'anonymous' | 'unavailable';
export interface NativeSessionObservation { readonly state: NativeSessionState; }
export interface BffSessionObserverOptions {
  privateRoots: HTMLElement[];
  userPath?: string;
  /** Called before private roots can become visible; throwing leaves them hidden. */
  onObservation?(observation: NativeSessionObservation): void;
  signal?: AbortSignal;
}
export interface BffSessionObserver {
  ready: Promise<NativeSessionObservation | { readonly state: 'superseded' }>;
  refresh(): Promise<NativeSessionObservation | { readonly state: 'superseded' }>;
  /** Cancels reads and removes handlers; private roots remain hidden. */
  dispose(): void;
}
/** Never writes browser storage, discards drafts, retries a domain operation or initiates logout. */
export function observeBffSession(options: BffSessionObserverOptions): BffSessionObserver;
