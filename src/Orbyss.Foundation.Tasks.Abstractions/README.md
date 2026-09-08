# Orbyss.Foundation.Tasks.Abstractions

Contracts for startup, background, and recurring work owned by one CShells shell generation.

Implement `IStartupTask`, `IBackgroundTask`, or `IRecurringTask` in a feature package and register it with the
matching `AddFoundation*Task<TTask>` extension. Enable the `FoundationTasks` feature in the shell. Startup tasks
run in a shell scope before activation completes; background and recurring tasks are cancelled and awaited when
that shell generation drains.

Do not register shell work with `AddHostedService`: the Generic Host starts only root-provider hosted services.