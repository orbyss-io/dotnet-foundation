using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Creates success results governed by the registered typed shell contract.</summary>
public interface IJsonResponseFactory<T>
{
    /// <summary>Creates a result that admits output before committing success headers.</summary>
    IResult Create(T value, int statusCode = StatusCodes.Status200OK);
}
