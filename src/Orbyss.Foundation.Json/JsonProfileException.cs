namespace Orbyss.Foundation.Json;

/// <summary>Reports a deterministic JSON admission failure without carrying authored payloads.</summary>
public sealed class JsonProfileException : Exception
{
    /// <summary>Creates a classified admission failure.</summary>
    public JsonProfileException(string code) : base(code) => Code = code;
    /// <summary>Gets the stable application-independent error code.</summary>
    public string Code { get; }
}
