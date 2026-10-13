using System.Net;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Recognizes only failures observed by the owned native OIDC transport or document retriever.</summary>
internal static class BffProviderFailureProvenance
{
    /// <summary>Marks actual transport failures without relying on exception messages or exposing a public classification key.</summary>
    private static readonly object ObservedFailure = new();

    /// <summary>Retains native exception identity and its cause for protocol wrappers.</summary>
    internal static void Mark(Exception failure) => failure.Data[ObservedFailure] = true;

    /// <summary>Limits availability recovery to demonstrated provider transport, HTTP timeout or availability status failures.</summary>
    internal static bool IsUnavailable(Exception failure)
    {
        var cancellationObserved = false;
        var timeoutObserved = false;
        for (var depth = 0; depth < 16; depth++, failure = failure.InnerException!)
        {
            if (failure is TaskCanceledException { InnerException: TimeoutException }) timeoutObserved = true;
            if (failure.Data.Contains(ObservedFailure))
            {
                if (failure is HttpRequestException request)
                    return request.StatusCode is null || IsAvailabilityStatus(request.StatusCode.Value);
                if (failure is IOException) return true;
                if (failure is OperationCanceledException) cancellationObserved = true;
            }
            // Native retrieval/client wrappers retain InnerException; arbitrary application errors are not wrappers.
            if (failure is not (InvalidOperationException or IOException or OperationCanceledException or TimeoutException)
                || failure.InnerException is null) break;
        }
        return cancellationObserved && timeoutObserved;
    }

    /// <summary>Separates transient availability responses from invalid client, address or protocol configuration.</summary>
    internal static bool IsAvailabilityStatus(HttpStatusCode status) => (int)status is 408 or 429 or >= 500 and <= 599;
}
