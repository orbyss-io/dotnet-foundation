namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Creates typed JSON contracts with named supported capacities and standard profile keys.</summary>
public static class JsonContractFactory
{
    /// <summary>Declares a request using the standard strict-request profile unless a custom key is supplied.</summary>
    /// <typeparam name="T">The request wire type.</typeparam>
    /// <param name="minimumBytes">Minimum supported configured byte capacity, independent of the deployment budget.</param>
    /// <param name="maximumBytes">Maximum supported configured byte capacity, up to 16 MiB.</param>
    /// <param name="minimumDepth">Minimum supported configured nesting depth.</param>
    /// <param name="maximumDepth">Maximum supported configured nesting depth, up to 64.</param>
    /// <param name="profile">Optional configured profile key; its preset must remain strict-request.</param>
    public static JsonContractMetadata<T> Request<T>(
        int minimumBytes = 1,
        int maximumBytes = 16_777_216,
        int minimumDepth = 1,
        int maximumDepth = 64,
        JsonProfileKey? profile = null
    ) => JsonContractMetadata<T>.Request(
        JsonProfileRequirement.StrictRequest(minimumBytes, maximumBytes, minimumDepth, maximumDepth),
        profile
    );

    /// <summary>Declares a response using the standard success-response profile unless a custom key is supplied.</summary>
    /// <typeparam name="T">The response wire type.</typeparam>
    /// <param name="minimumBytes">Minimum supported configured byte capacity, independent of the deployment budget.</param>
    /// <param name="maximumBytes">Maximum supported configured byte capacity, up to 16 MiB.</param>
    /// <param name="minimumDepth">Minimum supported configured nesting depth.</param>
    /// <param name="maximumDepth">Maximum supported configured nesting depth, up to 64.</param>
    /// <param name="profile">Optional configured profile key; its preset must remain tolerant-response.</param>
    public static JsonContractMetadata<T> Response<T>(
        int minimumBytes = 1,
        int maximumBytes = 16_777_216,
        int minimumDepth = 1,
        int maximumDepth = 64,
        JsonProfileKey? profile = null
    ) => JsonContractMetadata<T>.Response(
        JsonProfileRequirement.TolerantResponse(minimumBytes, maximumBytes, minimumDepth, maximumDepth),
        profile
    );
}
