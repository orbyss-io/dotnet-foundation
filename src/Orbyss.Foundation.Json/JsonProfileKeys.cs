namespace Orbyss.Foundation.Json;

/// <summary>Exports preserved preset identities and standard configured profile keys.</summary>
public static class JsonProfileKeys
{
    /// <summary>Strict request admission preset/key.</summary>
    public const string StrictRequest = "strict-request";
    /// <summary>Tolerant response reading preset/key.</summary>
    public const string TolerantResponse = "tolerant-response";
    /// <summary>Standard bounded success/page profile key.</summary>
    public const string SuccessResponse = "success-response";
    /// <summary>Standard bounded problem profile key.</summary>
    public const string ProblemResponse = "problem-response";
}
