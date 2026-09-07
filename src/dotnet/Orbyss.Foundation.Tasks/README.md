# Orbyss.Foundation.Tasks

`FoundationTasks` is the CShells feature that starts and drains Orbyss Foundation task contracts with each shell
generation. The package owns cancellation, fault logging, recurring timers, and asynchronous shutdown; feature
packages own the task implementations and register them through `Orbyss.Foundation.Tasks.Abstractions`.

The manager is a shell singleton. Startup tasks are resolved from an async scope, while background and recurring
tasks must be shell-safe singletons. A recurring task must declare a positive interval.