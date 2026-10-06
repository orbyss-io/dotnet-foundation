namespace Orbyss.Foundation.Json;

/// <summary>Reports server output exceeding its declared capacity. It is not a client request failure.</summary>
public sealed class JsonOutputLimitException : JsonResponseContractException
{
    /// <summary>Creates a safe capacity failure without retaining private payload data.</summary>
    public JsonOutputLimitException() : base(JsonFailureCodes.ResponseSizeExceeded) { }
}
