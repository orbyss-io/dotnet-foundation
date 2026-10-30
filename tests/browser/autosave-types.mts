import { AUTOSAVE_CONTRACT, createAutosaveController, type SaveAdapter, type SaveState }
  from '../../src/Orbyss.Foundation.Web.Presentation/browser/autosave.mjs';

const contract: 'foundation-browser-autosave/v1' = AUTOSAVE_CONTRACT;
type Receipt = Readonly<{ serverVersion: number }>;
const adapter: SaveAdapter<Receipt> = {
  async admit(context) { context.signal.throwIfAborted(); return 'ready'; },
  capture(identity) { return { kind: 'prepared', payload: JSON.stringify(identity) }; },
  async write() { return { kind: 'confirmed', receipt: { serverVersion: 1 } }; },
  async lookup() { return { kind: 'unknown' }; },
  confirmed(_attempt, receipt) { const version: number = receipt.serverVersion; void version; return undefined; },
};
const controller = createAutosaveController({ enabled: true, intervalMilliseconds: 1200000,
  operationTimeoutMilliseconds: 20000, signal: new AbortController().signal });
const target = controller.register({ target: 'opaque-id', maximumPayloadBytes: 1024,
  initial: { editRevision: 1, confirmedEditRevision: 0, generation: 'opaque-local-generation', pending: null }, adapter });
const unregister = target.subscribe((state: SaveState<Receipt>) => {
  const version: number | undefined = state.lastConfirmation?.receipt.serverVersion;
  void version;
  return undefined;
});
const stopActivity = controller.subscribeActivity(activity => {
  const busy: boolean = activity.busy;
  const draining: boolean = activity.draining;
  void busy; void draining;
  return undefined;
});
stopActivity();
// @ts-expect-error Activity does not authorize writes and is not mutable UI state.
controller.activity.busy = false;
// @ts-expect-error Controller activity observers finish synchronously.
controller.subscribeActivity(async () => undefined);
void target.save(); void target.reconcile('status'); void controller.dispose(); unregister();
// @ts-expect-error Original replay bytes are immutable.
if (target.state.pending) target.state.pending.payload = 'changed';
// @ts-expect-error Local acknowledgement cannot be edited by a consumer.
target.state.confirmedEditRevision = 99;
// @ts-expect-error A pending result is not an established server receipt.
void target.state.serverVersion;
// @ts-expect-error An asynchronous confirmation does not establish a synchronous application baseline.
const asynchronousConfirmation: SaveAdapter<Receipt>['confirmed'] = async () => undefined;
// @ts-expect-error Status absence cannot authorize a fresh dispatch.
const unsafeStatus: NonNullable<SaveAdapter<Receipt>['lookup']> = async () => ({ kind: 'rejected-without-effect' });
// @ts-expect-error Observers cannot start unobserved asynchronous publication.
target.subscribe(async () => undefined);
// @ts-expect-error Reconciliation modes are selected capabilities, not arbitrary retry strings.
void target.reconcile('retry-new');
// @ts-expect-error Request transport cannot bypass the controller slot through its target handle.
void target.write;
void contract; void asynchronousConfirmation; void unsafeStatus;
