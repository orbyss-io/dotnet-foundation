# Orbyss Foundation PostgreSQL

The consumer's persistence feature registers its native context with `AddFoundationPostgreSql<TContext>`.
Typed policies bind from `Foundation:PostgreSql:Policies:<name>` or are supplied explicitly. A stable
keyed `NpgsqlDataSource` belongs to each shell provider generation and policy. Context pooling is not used.
Connection, command, lock and cancellation acknowledgement settings are validated once; operation stage
caps do not rewrite connection strings or create additional pool identities.

Contexts derive from `FoundationPostgreSqlDbContext` in their persistence project. Its sealed native
disposal methods release factory reservations only after EF disposal completes, including concurrent
and repeated disposal. EF has no context-disposal interceptor, so this intentional native base establishes
the lifetime guarantee without reflecting internal state or trusting an extra caller callback.
The context constructor must pass the admitted options to its base unchanged. Provider selection is
sealed in `OnConfiguring`; application entities and mappings remain in the context's `OnModelCreating`.

Resolve `IPostgreSqlUnitLeaseFactory<TContext>` and `await using` its unit, then independently `await using`
each context created through `unit.Factory.CreateDbContextAsync`. Units begin actual `IShell.BeginScope`
on the exact owning generation; ordinary root/plain-DI factory creation is rejected. Initialization,
background work and evaluation cleanup use the same path. Closing a unit stops admission but retains its
tracked scope until existing contexts dispose. Datasource teardown also waits for its context holds.
Concurrent and repeated owner disposal observe the same owner-close outcome, including delayed or
failed final scope disposal. An early close with live contexts stops admission immediately; those
contexts own final scope release and any failure from that release.

Use the deadline token for database calls and pass an existing outer deadline to follow-up/reconciliation
units. Caller cancellation does not establish rollback or commit certainty. The package owns no schema,
entities, transaction contents, retry policy or domain outcome mapping. `PostgreSqlFailureInfo` exposes
only SQLSTATE and constraint identity for consumer-owned classification; it excludes provider messages and SQL.
Pass `unit.Deadline.Token` explicitly to `BeginTransactionAsync`, `CommitAsync` and `RollbackAsync`.
Native transaction dispatch has no automatic command-stage cap; a consumer may own a narrower stage
when its policy requires one. Async EF command dispatch receives the capped stage token, and sync
completion checks elapsed time. A canceled sync command may already have committed; reconcile its
receipt through a fresh unit instead of inferring rollback. Native reader-close events release command
stages after drain/cleanup even when EF's later disposal notification is skipped by a close fault.

Provider-owned EF and Npgsql logging preserves native categories, event IDs and severity plus safe
exception type/SQLSTATE classifications. It discards native SQL/parameter state, raw exception text and
native scope payloads, and explicitly disables parameter logging on each stable datasource. Application
ambient correlation remains available through the destination logging factory. The package does not
change process-global logging configuration or take ownership of the application's logger factory.

The native qualifier uses an owned ephemeral PostgreSQL container with no retained volume. Its explicit
`--deadline-only` mode exercises deterministic deadlines without Docker and does not claim database acceptance.
