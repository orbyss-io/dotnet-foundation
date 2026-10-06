# Orbyss Foundation Execution

Registers a replaceable `TimeProvider` and `IExecutionDeadlineFactory`. Dispose each operation deadline
and each stage. Remaining time uses monotonic timestamps; UTC clock adjustments do not extend an operation.
Every stage is capped by the outer remaining time, including retries. Caller cancellation is reported by
the token; `IsExpired` identifies time expiry separately. Disposing a deadline cancels its owned stages.
These mechanisms do not map cancellation to rollback, commit certainty or application outcomes.
