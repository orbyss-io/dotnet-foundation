using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.Web.ProblemDetails;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Declares known wire types and enforces registered response-profile use per operation.</summary>
public static class FoundationJsonEndpointExtensions
{
    /// <summary>Adds request metadata from the registered typed requirement without duplicated numbers.</summary>
    public static RouteHandlerBuilder WithJsonRequest<T>(this RouteHandlerBuilder builder) where T : notnull
    {
        builder.Accepts<T>("application/json");
        builder.Add(endpoint => endpoint.Metadata.Add(Resolve<T>(endpoint.ApplicationServices, JsonContractDirection.Request)));
        builder.AddEndpointFilterFactory((context, next) =>
        {
            var requestMetadata = Resolve<T>(context.ApplicationServices, JsonContractDirection.Request);
            if (context.MethodInfo.GetParameters().Any(parameter => parameter.ParameterType == typeof(T)))
                throw new InvalidOperationException("Declared JSON requests must use the registered typed reader instead of native body binding.");
            return async invocation =>
            {
                var http = invocation.HttpContext;
                var admission = http.Features.Get<JsonAdmissionState>() ?? new JsonAdmissionState();
                admission.RequestType = typeof(T);
                admission.RequestProfile = requestMetadata.Profile;
                http.Features.Set(admission);
                var original = http.Request.Body;
                var originalReader = http.Request.BodyReader;
                var originalPipeFeature = http.Features.Get<IRequestBodyPipeFeature>();
                http.Request.Body = new JsonAdmissionStream(original, admission, request: true);
                http.Features.Set<IRequestBodyPipeFeature>(new JsonAdmissionRequestBodyPipeFeature(new JsonAdmissionPipeReader(originalReader, admission)));
                try
                {
                    var result = await next(invocation).ConfigureAwait(false);
                    var admittedFailure = FoundationProblemResults.IsAdmittedFailure(result as IResult)
                        || result is JsonAdmissionResult { IsProblem: true };
                    if (admission.Failed || (!admission.RequestAdmitted && !admittedFailure))
                        throw new JsonResponseContractException(JsonFailureCodes.RequestProfileBypass);
                    return result;
                }
                finally { http.Request.Body = original; http.Features.Set(originalPipeFeature); }
            };
        });
        return builder;
    }
    /// <summary>Adds native response metadata and rejects successful results bypassing the declared profile.</summary>
    public static RouteHandlerBuilder WithJsonResponse<T>(this RouteHandlerBuilder builder, int statusCode = StatusCodes.Status200OK)
    {
        builder.Produces<T>(statusCode, "application/json");
        builder.Add(endpoint => endpoint.Metadata.Add(Resolve<T>(endpoint.ApplicationServices, JsonContractDirection.Response)));
        builder.AddEndpointFilterFactory((context, next) =>
        {
            var metadata = Resolve<T>(context.ApplicationServices, JsonContractDirection.Response);
            return async invocation =>
            {
                var http = invocation.HttpContext;
                var admission = http.Features.Get<JsonAdmissionState>() ?? new JsonAdmissionState();
                http.Features.Set(admission);
                var original = http.Features.GetRequiredFeature<IHttpResponseBodyFeature>();
                admission.OriginalResponseBody ??= original;
                http.Features.Set<IHttpResponseBodyFeature>(new JsonAdmissionResponseBodyFeature(original, admission));
                try
                {
                    var result = await next(invocation).ConfigureAwait(false);
                    if (admission.Failed) throw new JsonResponseContractException(JsonFailureCodes.ResponseProfileBypass);
                    if (result is IResult failure && FoundationProblemResults.IsAdmittedFailure(failure))
                        return new JsonAdmissionResult(failure, original, admission, problem: true);
                    if (result is not IJsonProfileResult admitted || admitted.ContractType != typeof(T) || admitted.Profile != metadata.Profile)
                        throw new JsonResponseContractException(JsonFailureCodes.ResponseProfileBypass);
                    return new JsonAdmissionResult((IResult)result, original, admission, problem: false);
                }
                catch { http.Features.Set(original); throw; }
            };
        });
        return builder;
    }
    /// <summary>Requires exactly one contract so registration cannot silently replace or omit metadata.</summary>
    private static IJsonContractMetadata Resolve<T>(IServiceProvider provider, JsonContractDirection direction)
    {
        var matches = provider.GetServices<IJsonContractMetadata>().Where(metadata => metadata.ContractType == typeof(T) && metadata.Direction == direction).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Endpoint requires exactly one registered typed JSON contract.");
        return matches[0];
    }
}
