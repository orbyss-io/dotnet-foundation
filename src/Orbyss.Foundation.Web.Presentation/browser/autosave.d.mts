/** Optional editor-local coordination; adapters establish server outcomes. */
export declare const AUTOSAVE_CONTRACT: 'foundation-browser-autosave/v1';

export type SavePhase = 'clean' | 'dirty' | 'pending' | 'unknown' | 'conflict' | 'rejected';
export type SaveTrigger = 'manual' | 'automatic' | 'status' | 'replay';
export type Attempt = Readonly<{
  target: string;
  operationKey: string;
  editRevision: number;
  generation: string;
  payload: string;
}>;
export type OperationContext = Readonly<{
  signal: AbortSignal;
  expiresAtMonotonic: number;
  trigger: SaveTrigger;
}>;
export type Confirmed<R> = Readonly<{ kind: 'confirmed'; receipt: R }>;
export type Unknown = Readonly<{ kind: 'unknown' }>;
export type SaveResult<R> = Confirmed<R> | Unknown
  | Readonly<{ kind: 'rejected-without-effect' }>
  | Readonly<{ kind: 'conflict' }>;
export type SaveState<R> = Readonly<{
  phase: SavePhase;
  editRevision: number;
  confirmedEditRevision: number;
  generation: string;
  pending: Attempt | null;
  lastConfirmation: Readonly<{ attempt: Attempt; receipt: R }> | null;
  fault: 'adapter' | null;
}>;
export type InitialSaveState = Readonly<{
  editRevision: number;
  confirmedEditRevision: number;
  generation: string;
  pending: Attempt | null;
}>;
export interface SaveAdapter<R> {
  admit(context: OperationContext): Promise<'ready' | 'blocked'>;
  capture(identity: Omit<Attempt, 'payload'>):
    | Readonly<{ kind: 'prepared'; payload: string }>
    | Readonly<{ kind: 'blocked' | 'invalid' }>;
  prepare?(attempt: Attempt, context: OperationContext):
    Promise<'stored' | 'unavailable'>;
  write(attempt: Attempt, context: OperationContext): Promise<SaveResult<R>>;
  lookup?(attempt: Attempt, context: OperationContext): Promise<Confirmed<R> | Unknown>;
  replay?(attempt: Attempt, context: OperationContext): Promise<SaveResult<R>>;
  confirmed(attempt: Attempt, receipt: R): undefined;
}
export type RequestResult = 'observed' | 'clean' | 'busy' | 'blocked' | 'disposed';
export interface SaveTarget<R> {
  readonly state: SaveState<R>;
  edited(): void;
  save(): Promise<RequestResult>;
  reconcile(mode: 'status' | 'replay'): Promise<RequestResult>;
  resumeAfterConflict(): void;
  unsaved(): 'clean' | 'dirty' | 'unknown';
  subscribe(listener: (state: SaveState<R>) => undefined): () => void;
  unregister(): Promise<void>;
}
export interface AutosaveController {
  readonly activity: ControllerActivity;
  subscribeActivity(listener: (activity: ControllerActivity) => undefined): () => void;
  register<R>(options: {
    target: string;
    maximumPayloadBytes: number;
    initial: InitialSaveState;
    adapter: SaveAdapter<R>;
  }): SaveTarget<R>;
  suspend(): void;
  resume(): void;
  dispose(): Promise<void>;
}
/** Local adapter activity, independent of whether a business effect committed or the server stopped. */
export type ControllerActivity = Readonly<{
  busy: boolean;
  draining: boolean;
  suspended: boolean;
  disposed: boolean;
}>;
export declare function createAutosaveController(options: {
  enabled: boolean;
  intervalMilliseconds: number;
  operationTimeoutMilliseconds: number;
  signal?: AbortSignal;
}): AutosaveController;
