namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Separates client request admission from server response obligations.</summary>
public enum JsonContractDirection
{
    /// <summary>Client input.</summary>
    Request,
    /// <summary>Server output.</summary>
    Response
}
