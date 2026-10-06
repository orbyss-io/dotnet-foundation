namespace Orbyss.Foundation.Json;

/// <summary>Reports a server output contract failure without exposing its payload or serializer details.</summary>
public class JsonResponseContractException : Exception
{
    /// <summary>Gets the stable server failure classification.</summary>
    public string Code { get; }
    /// <summary>Creates a safe output failure classification.</summary>
    public JsonResponseContractException(string code) : base("Server JSON response contract failed.") => Code = code;
}
