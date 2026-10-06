using Microsoft.AspNetCore.Http.Features;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Coordinates typed parser/result admission with operation-owned body guards.</summary>
internal sealed class JsonAdmissionState
{
    /// <summary>Permits body reads only while the registered reader owns parsing.</summary>
    public bool Reading { get; set; }
    /// <summary>Identifies the endpoint's declared request type.</summary>
    public Type? RequestType { get; set; }
    /// <summary>Identifies the endpoint's declared request profile.</summary>
    public JsonProfileKey RequestProfile { get; set; }
    /// <summary>Records successful admission through the registered typed reader.</summary>
    public bool RequestAdmitted { get; set; }
    /// <summary>Permits body writes only after bounded serialization has succeeded.</summary>
    public bool Writing { get; set; }
    /// <summary>Retains transport ownership for restoration on admission failure.</summary>
    public IHttpResponseBodyFeature? OriginalResponseBody { get; set; }
    /// <summary>Retains attempted bypass even when application code catches its exception.</summary>
    public bool Failed { get; private set; }
    /// <summary>Checks typed request ownership before consuming any input.</summary>
    public void RequireRead()
    {
        if (Reading) return;
        Failed = true;
        throw new JsonResponseContractException(JsonFailureCodes.RequestProfileBypass);
    }
    /// <summary>Opens only the declared parser and rejects concurrent/mismatched readers.</summary>
    public void BeginRead(Type type, JsonProfileKey profile)
    {
        if (Reading || (RequestType is not null && (type != RequestType || profile != RequestProfile)))
        {
            Failed = true;
            throw new JsonResponseContractException(JsonFailureCodes.RequestProfileBypass);
        }
        Reading = true;
    }
    /// <summary>Checks successful output admission before reserving/writing response bytes.</summary>
    public void RequireWrite()
    {
        if (Writing && !Failed) return;
        Failed = true;
        throw new JsonResponseContractException(JsonFailureCodes.ResponseProfileBypass);
    }
}
