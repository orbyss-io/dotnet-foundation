using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Combines an explicitly owned custom numeric representation with an ordinary nullable number.</summary>
internal sealed record CustomNumbers(int? Number, long? Ordinary)
{
    /// <summary>Selects only the profile whose allowlisted converter owns the custom representation.</summary>
    internal static JsonContractMetadata<CustomNumbers> Contract { get; } = CreateContract();

    /// <summary>Keeps the named profile selection outside shell composition.</summary>
    private static JsonContractMetadata<CustomNumbers> CreateContract()
    {
        var profile = new JsonProfileKey("custom-integer");
        return JsonContractMetadata<CustomNumbers>.Response(profile: profile);
    }
}
