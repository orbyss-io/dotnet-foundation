using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Admits a bounded closed envelope before committing through ASP.NET Problem Details.</summary>
internal sealed class FoundationBoundedProblemDetailsWriter : IProblemDetailsWriter
{
    /// <summary>The owning provider's immutable output byte budget.</summary>
    private readonly JsonProfile profile;
    /// <summary>A fixed minimal nonrecursive error encoder within every admitted deployment budget.</summary>
    private readonly JsonProfile fallback = new(new JsonProfileSettings
        { Preset = JsonProfileKeys.TolerantResponse, MaxBytes = FoundationProblemResponseOptions.MinimumBytes,
            MaxDepth = FoundationProblemResponseOptions.MinimumDepth });
    /// <summary>Records safe classifications without exception messages or raw exceptions.</summary>
    private readonly ILogger<FoundationBoundedProblemDetailsWriter> logger;
    /// <summary>Admits the deployment budget when the writer is resolved by its shell provider.</summary>
    public FoundationBoundedProblemDetailsWriter(IOptions<FoundationProblemResponseOptions> options,
        ILogger<FoundationBoundedProblemDetailsWriter> logger)
    {
        if (options.Value.MaxBytes is < FoundationProblemResponseOptions.MinimumBytes or > 16_777_216
            || options.Value.MaxDepth is < FoundationProblemResponseOptions.MinimumDepth or > 64
            || options.Value.Preset != JsonProfileKeys.TolerantResponse)
            throw new OptionsValidationException(Options.DefaultName, typeof(FoundationProblemResponseOptions),
                ["Problem response requires tolerant-response, 512 bytes to 16 MiB, and depth 3 to 64."]);
        profile = new JsonProfile(new JsonProfileSettings
            { Preset = options.Value.Preset, MaxBytes = options.Value.MaxBytes, MaxDepth = options.Value.MaxDepth });
        this.logger = logger;
    }
    /// <inheritdoc />
    public bool CanWrite(ProblemDetailsContext context) => true;
    /// <inheritdoc />
    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var http = context.HttpContext;
        if (http.Response.HasStarted)
            throw new InvalidOperationException("Cannot replace an already committed problem response.");
        http.RequestAborted.ThrowIfCancellationRequested();
        http.RequestServices.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails?.Invoke(context);
        byte[] bytes;
        try
        {
            var problem = context.ProblemDetails;
            var definition = new ProblemDefinition(statusCode: problem.Status ?? 500,
                code: (string)problem.Extensions[ProblemExtensionNames.Code]!, title: problem.Title,
                detail: problem.Detail,
                fieldErrors: (IEnumerable<ProblemFieldError>)problem.Extensions[ProblemExtensionNames.FieldErrors]!);
            var correlation = AdmitCorrelation((string)problem.Extensions[ProblemExtensionNames.CorrelationId]!);
            bytes = profile.Serialize(new FoundationProblemEnvelope(Type: "about:blank",
                Title: definition.Title ?? "Request Failed", Status: definition.StatusCode, Code: definition.Code,
                CorrelationId: correlation, TraceId: correlation, FieldErrors: definition.FieldErrors, Detail: definition.Detail),
                profile.TypeInfo<FoundationProblemEnvelope>(), http.RequestAborted);
        }
        catch (Exception error) when (error is JsonResponseContractException or ArgumentException
            or InvalidOperationException or InvalidCastException or KeyNotFoundException)
        {
            var correlation = AdmitCorrelation(http.TraceIdentifier);
            logger.LogError("Foundation problem output rejected {FailureType} with correlation {CorrelationId}.",
                error.GetType().Name, correlation);
            bytes = fallback.Serialize(new FoundationProblemEnvelope(Type: "about:blank", Title: "Request Failed",
                Status: 500, Code: ProblemCodes.RequestFailed, CorrelationId: correlation, TraceId: correlation,
                FieldErrors: Array.Empty<ProblemFieldError>(), Detail: null),
                fallback.TypeInfo<FoundationProblemEnvelope>(), http.RequestAborted);
            context.ProblemDetails.Status = 500;
        }
        http.RequestAborted.ThrowIfCancellationRequested();
        http.Response.StatusCode = context.ProblemDetails.Status ?? 500;
        http.Response.ContentType = "application/problem+json";
        http.Response.Headers.CacheControl = "no-store";
        http.Response.ContentLength = bytes.Length;
        await http.Response.Body.WriteAsync(bytes, http.RequestAborted).ConfigureAwait(false);
    }
    /// <summary>Prevents correlation content from exceeding the finite fallback envelope or carrying control text.</summary>
    private static string AdmitCorrelation(string value) => value.Length is >= 1 and <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or ':' or '.')
        ? value : Guid.NewGuid().ToString("N");
}
