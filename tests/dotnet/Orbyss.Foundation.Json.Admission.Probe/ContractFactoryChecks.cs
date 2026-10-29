using Orbyss.Foundation.Json.Admission.Probe.Operations.ChangeValue;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Checks public factory defaults, independently named overrides and invalid capacity rejection.</summary>
internal static class ContractFactoryChecks
{
    /// <summary>Exercises both directions before the same metadata participates in native registration and HTTP checks.</summary>
    internal static void Run()
    {
        VerifyDefaults();
        VerifyOverrides();
        VerifyInvalidIntervals();
    }

    /// <summary>Retains standard keys, directions, wire ownership and all existing supported interval defaults.</summary>
    private static void VerifyDefaults()
    {
        var request = JsonContractFactory.Request<Request>();
        var response = JsonContractFactory.Response<Request>();
        Require(request.Profile == new JsonProfileKey(JsonProfileKeys.StrictRequest)
            && request.Direction == JsonContractDirection.Request
            && request.Requirement.Preset == JsonProfileKeys.StrictRequest, "request factory changed its standard profile or direction");
        Require(response.Profile == new JsonProfileKey(JsonProfileKeys.SuccessResponse)
            && response.Direction == JsonContractDirection.Response
            && response.Requirement.Preset == JsonProfileKeys.TolerantResponse, "response factory confused its configured key and preset");
        foreach (var contract in new[] { request, response })
        {
            var supported = contract.Requirement;
            Require(contract.ContractType == typeof(Request) && supported.MinimumBytes == 1
                && supported.MaximumBytes == 16_777_216 && supported.MinimumDepth == 1
                && supported.MaximumDepth == 64, "factory changed the default supported wire contract");
        }
    }

    /// <summary>Preserves custom profile keys and every named capacity parameter without changing strictness.</summary>
    private static void VerifyOverrides()
    {
        var requestKey = new JsonProfileKey("custom-request");
        var responseKey = new JsonProfileKey("custom-response");
        var request = JsonContractFactory.Request<Request>(minimumBytes: 16, maximumBytes: 4096,
            minimumDepth: 2, maximumDepth: 12, profile: requestKey);
        var response = JsonContractFactory.Response<Request>(minimumBytes: 8, maximumBytes: 8192,
            minimumDepth: 3, maximumDepth: 16, profile: responseKey);
        Require(request.Profile == requestKey && request.Requirement.Preset == JsonProfileKeys.StrictRequest
            && request.Requirement.MinimumBytes == 16 && request.Requirement.MaximumBytes == 4096
            && request.Requirement.MinimumDepth == 2 && request.Requirement.MaximumDepth == 12,
            "request factory lost a named override");
        Require(response.Profile == responseKey && response.Requirement.Preset == JsonProfileKeys.TolerantResponse
            && response.Requirement.MinimumBytes == 8 && response.Requirement.MaximumBytes == 8192
            && response.Requirement.MinimumDepth == 3 && response.Requirement.MaximumDepth == 16,
            "response factory lost a named override");
    }

    /// <summary>Rejects invalid and reversed intervals in both directions through the original requirement validation.</summary>
    private static void VerifyInvalidIntervals()
    {
        foreach (var (minimumBytes, maximumBytes, minimumDepth, maximumDepth) in new[]
        {
            (0, 4096, 1, 64), (4096, 1024, 1, 64), (1, 16_777_217, 1, 64),
            (1, 4096, 0, 64), (1, 4096, 8, 4), (1, 4096, 1, 65)
        })
        {
            Reject(() => JsonContractFactory.Request<Request>(minimumBytes, maximumBytes, minimumDepth, maximumDepth));
            Reject(() => JsonContractFactory.Response<Request>(minimumBytes, maximumBytes, minimumDepth, maximumDepth));
        }
    }

    /// <summary>Requires the intended capacity diagnostic instead of accepting an unrelated exception.</summary>
    private static void Reject(Action create)
    {
        try
        {
            create();
        }
        catch (ArgumentException error) when (error.Message == "JSON contract has invalid supported requirements.")
        {
            return;
        }
        throw new InvalidOperationException("JSON contract factory accepted an invalid supported interval.");
    }

    /// <summary>Reports a failed public contract observation.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
