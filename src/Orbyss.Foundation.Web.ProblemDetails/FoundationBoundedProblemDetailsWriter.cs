using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Web.ProblemDetails.Core;
using System.Text;

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
        AdmittedProblemPage page;
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
                CorrelationId: correlation, TraceId: correlation,
                FieldErrors: definition.FieldErrors.Select(field => new FoundationProblemFieldError(field.Field, field.Code, field.Message)).ToArray(),
                Detail: definition.Detail),
                profile.TypeInfo<FoundationProblemEnvelope>(), http.RequestAborted);
            page = new AdmittedProblemPage(definition.StatusCode, definition.Code,
                definition.Title ?? "Request Failed", correlation, AdmitPathBase(http),
                http.RequestServices.GetService<IProblemPageSessionReader>()?.ReadLocalSession(http), AdmitLocales(http));
        }
        catch (Exception error) when (error is JsonResponseContractException or ArgumentException
            or InvalidOperationException or InvalidCastException or KeyNotFoundException)
        {
            var correlation = AdmitCorrelation(http.TraceIdentifier);
            logger.LogError("Foundation problem output rejected {FailureType} with correlation {CorrelationId}.",
                error.GetType().Name, correlation);
            bytes = fallback.Serialize(new FoundationProblemEnvelope(Type: "about:blank", Title: "Request Failed",
                Status: 500, Code: ProblemCodes.RequestFailed, CorrelationId: correlation, TraceId: correlation,
                FieldErrors: Array.Empty<FoundationProblemFieldError>(), Detail: null),
                fallback.TypeInfo<FoundationProblemEnvelope>(), http.RequestAborted);
            context.ProblemDetails.Status = 500;
            page = new AdmittedProblemPage(500, ProblemCodes.RequestFailed, "Request Failed", correlation, AdmitPathBase(http), preferredLocales: AdmitLocales(http));
        }
        var contentType = "application/problem+json";
        string? contentLanguage = null;
        if (ProblemPageNegotiation.AllowsHtml(http))
        {
            try
            {
                var renderer = http.RequestServices.GetService<IProblemPageRenderer>();
                var html = renderer?.Render(page);
                if (html is not null)
                {
                    var encoding = new UTF8Encoding(false, true);
                    if (encoding.GetByteCount(html) > profile.MaxBytes) throw new InvalidOperationException("Page output exceeds the problem response budget.");
                    bytes = encoding.GetBytes(html);
                    contentLanguage = renderer!.GetContentLanguage(page);
                    if (contentLanguage is not null && (contentLanguage.Length is < 1 or > 64
                        || System.Globalization.CultureInfo.GetCultureInfo(contentLanguage).Name != contentLanguage))
                        throw new InvalidOperationException("Problem page content language is invalid.");
                    contentType = "text/html; charset=utf-8";
                }
            }
            catch (Exception error) when (error is not OperationCanceledException || !http.RequestAborted.IsCancellationRequested)
            {
                logger.LogError("Foundation problem page rejected {FailureType} with correlation {CorrelationId}.",
                    error.GetType().Name, page.CorrelationId);
                context.ProblemDetails.Status = 500;
                // Fixed nonrecursive HTML fits the minimum 512-byte budget. It
                // never calls the failed renderer or exposes its exception.
                bytes = Encoding.UTF8.GetBytes("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>Request failed</title>"
                    + "<main><h1>Request failed</h1><p>Please try again later.</p><p>Reference: " + page.CorrelationId + "</p></main></html>");
                contentType = "text/html; charset=utf-8";
                contentLanguage = "en";
            }
        }
        http.RequestAborted.ThrowIfCancellationRequested();
        http.Response.StatusCode = context.ProblemDetails.Status ?? 500;
        http.Response.ContentType = contentType;
        http.Response.Headers.CacheControl = "no-store";
        if (contentLanguage is not null)
        {
            http.Response.Headers.ContentLanguage = contentLanguage;
            http.Response.Headers.Vary = "Accept-Language";
        }
        if (contentType.StartsWith("text/html", StringComparison.Ordinal))
        {
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
            http.Response.Headers["Referrer-Policy"] = "no-referrer";
        }
        http.Response.ContentLength = bytes.Length;
        if (!HttpMethods.IsHead(http.Request.Method))
            await http.Response.Body.WriteAsync(bytes, http.RequestAborted).ConfigureAwait(false);
    }
    /// <summary>Prevents correlation content from exceeding the finite fallback envelope or carrying control text.</summary>
    private static string AdmitCorrelation(string value) => value.Length is >= 1 and <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or ':' or '.')
        ? value : Guid.NewGuid().ToString("N");
    /// <summary>Admits only bounded canonical culture names, retaining language weights without exposing request text to renderers.</summary>
    private static IReadOnlyList<string> AdmitLocales(HttpContext context)
    {
        var result = new List<string>();
        try
        {
            foreach (var item in (context.Request.GetTypedHeaders().AcceptLanguage ?? [])
                .Where(item => (item.Quality ?? 1) > 0 && item.Value.Length <= 64).OrderByDescending(item => item.Quality ?? 1).Take(32))
            {
                try
                {
                    var culture = System.Globalization.CultureInfo.GetCultureInfo(item.Value.ToString());
                    if (culture.Name.Length > 0) result.Add(culture.Name);
                }
                catch (System.Globalization.CultureNotFoundException) { }
            }
        }
        catch (FormatException) { }
        return result.AsReadOnly();
    }
    /// <summary>Retains the native shell prefix after CShells restores outer endpoint paths.</summary>
    private static string AdmitPathBase(HttpContext context)
    {
        var value = (context.Features.Get<ProblemRequestPathBase>()?.Value
            ?? context.Features.Get<IAuthenticationFeature>()?.OriginalPathBase ?? context.Request.PathBase).Value ?? "";
        return value.Length <= 1024 && (value.Length == 0 || value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal))
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_' or '.')
            && !value.Split('/').Any(segment => segment is "." or "..") ? value.TrimEnd('/') : "";
    }
}
