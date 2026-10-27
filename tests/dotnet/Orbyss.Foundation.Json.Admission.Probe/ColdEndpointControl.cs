namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Has an intentionally absent runtime dependency so cold registration cannot accidentally create it.</summary>
internal sealed class ColdEndpointControl
{
    /// <summary>Counts actual endpoint activation.</summary>
    internal static int Created;

    /// <summary>Requires an application service absent from metadata-only composition.</summary>
    public ColdEndpointControl(IServiceScope missingRuntimeService)
    {
        Created++;
    }
}
