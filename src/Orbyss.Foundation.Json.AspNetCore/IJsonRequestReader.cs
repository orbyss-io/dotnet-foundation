using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Reads the actual body through the registered typed shell contract.</summary>
public interface IJsonRequestReader<T>
{
    /// <summary>Reads finite strict input with request/caller cancellation.</summary>
    Task<T> ReadAsync(HttpContext context, CancellationToken cancellationToken = default);
}
