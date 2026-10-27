namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Rejects conflicting declarations rather than selecting the last registered contract.</summary>
internal static class JsonContractIdentity
{
    /// <summary>Requires the same wire type, direction, profile and complete supported requirement.</summary>
    internal static void RequireEquivalent(IJsonContractMetadata first, IJsonContractMetadata second)
    {
        var expected = first.Requirement;
        var actual = second.Requirement;
        var equivalent = first.ContractType == second.ContractType && first.Direction == second.Direction
            && first.Profile == second.Profile && expected.Preset == actual.Preset
            && expected.MinimumBytes == actual.MinimumBytes && expected.MaximumBytes == actual.MaximumBytes
            && expected.MinimumDepth == actual.MinimumDepth && expected.MaximumDepth == actual.MaximumDepth;

        if (!equivalent)
            throw new InvalidOperationException("Conflicting typed JSON contract declarations for the same wire type and direction.");
    }
}
