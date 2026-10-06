using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Registers one explicit request/response contract independently of test configuration numbers.</summary>
internal static class AdmissionContracts
{
    /// <summary>Selects known profiles and supported capacities for the probe's actual message.</summary>
    internal static void Register(IServiceCollection services)
    {
        services.AddJsonRequestContract<Message>(new(JsonProfileKeys.StrictRequest),
            new(JsonProfileKeys.StrictRequest, maximumBytes: 1024));
        services.AddJsonResponseContract<Message>(new(JsonProfileKeys.SuccessResponse),
            new(JsonProfileKeys.TolerantResponse, maximumBytes: 1024));
    }
}
