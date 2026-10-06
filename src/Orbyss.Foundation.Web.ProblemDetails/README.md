# Orbyss.Foundation.Web.ProblemDetails

An optional CShells middleware feature for Orbyss Foundation's default Problem Details exception and
empty-status response format. Global exception dispatch is independently selected, so a consumer
can deactivate the feature and activate a custom global exception feature.

`AddFoundationProblemDetails()` registers the shared ASP.NET `IProblemDetailsWriter`, bounded
representation and request-time `IProblemDetailsEnricher` policy. Authentication and JSON select it
independently of optional global exception middleware. `FoundationProblemResults.Problem` accepts a
validated immutable `ProblemDefinition`, or an application-owned `IProblemMapper<TFailure>`.
Enrichers may localize safe text and fields while preserving code and status. They resolve from
`HttpContext.RequestServices`; singleton native exception handlers capture no scoped contributions.

`UseFoundationProblemStatusCodePages()` composes native ASP.NET empty-status handling without
activating exception handlers. The managed Host uses native ASP.NET status-code pages and registers
no global problem writer. Requests selecting Foundation mechanisms retain their shell's bounded DI
policy and budgets; unowned failures use ASP.NET's native text fallback. Explicit `WebRouting:Path`
selects prefix fallback ownership, and an empty path opts into root fallback. With no Foundation
mechanism selected, an application can register its own native `IProblemDetailsService` or register
a custom writer before `AddProblemDetails` without removing Host defaults. The Host shares only
the two CShells contract assemblies; this package loads and discovers its optional feature through
the normal native catalog. Already written responses are preserved. The native Foundation writer
marks admitted problems `Cache-Control: no-store`.

All paths use `code`, canonical `correlationId`, compatibility alias `traceId` and a bounded
`fieldErrors` array. Both correlation fields have the same admitted request identifier. Existing
0.2.4 generic codes are retained. Arbitrary extension objects, instance paths and exception messages
are excluded from the closed shape. The representation must contain safe public text, not submitted
values, SQL or private provider details.

The native writer admits the complete envelope before committing headers. Json.AspNetCore projects
its configured `problem-response` profile byte/depth/preset selection into `FoundationProblemResponseOptions`;
without that feature the writer defaults to 64 KiB, depth32 and tolerant-response. Budgets below 512 bytes
or above 16 MiB, depth below3 or above64, and a different preset fail activation. The minimum depth admits
the closed diagnostic array/object shape. Application profile resolvers/extensions do not control the
internal envelope; its fixed closed encoder consumes the admitted preset and byte/depth limits.
Oversized/invalid server output becomes a safe 500 envelope of at most 512 bytes through a fixed
nonrecursive fallback. Cancellation propagates and committed output cannot be replaced.

Selecting Authentication, Json.AspNetCore or this feature deliberately selects the bounded closed
Foundation representation. Application `IProblemDetailsEnricher` contributions and a replacement
`IProblemRepresentationPolicy` must preserve its validated shape and failure identity. Arbitrary
custom writer formats are not a bypass of selected typed JSON budgets.

The optional feature uses native ordered `IExceptionHandler` dispatch for `BadHttpRequestException`
and native fallback for unknown defects. An arbitrary `ArgumentException` remains 500. Native handled
diagnostics are suppressed because raw exceptions may contain private data; Foundation emits safe
failure/admission/output classifications and correlation instead. An exception after headers start
is rethrown by ASP.NET and aborts the response. Foundation.Host selects a narrow logger-factory
decorator for the native unhandled-error events in ExceptionHandlerMiddleware, DeveloperExceptionPageMiddleware
and Kestrel. It retains event ID, category, severity, safe failure type and admitted connection/correlation
IDs while dropping raw exception/state/formatter content. It does not suppress entire error categories.
This feature cannot retract committed output. Application logs, external telemetry, additional logger
factories and log scopes containing private values remain their owner's redaction responsibility.
