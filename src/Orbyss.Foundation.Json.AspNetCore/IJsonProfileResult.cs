namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Marks admitted results for the mandatory endpoint response filter.</summary>
internal interface IJsonProfileResult
{
    /// <summary>Gets the actual wire contract.</summary>
    Type ContractType { get; }
    /// <summary>Gets the selected configured profile.</summary>
    JsonProfileKey Profile { get; }
}
