using Microsoft.Extensions.Caching.Distributed;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>A distributed cache that can renew a live BFF ticket atomically with expiration and removal.</summary>
/// <remarks>
/// Custom distributed session backends implement this capability on their registered
/// <see cref="IDistributedCache"/>. The default exact MemoryDistributedCache implementation
/// is synchronized natively and does not require a custom provider.
/// </remarks>
public interface IBffSessionTicketCache : IDistributedCache
{
    /// <summary>Replaces a live ticket only if its current bytes still match the previously read bytes.</summary>
    /// <param name="key">The owning shell's opaque session key.</param>
    /// <param name="expectedTicket">The exact previously read value, compared by content.</param>
    /// <param name="renewedTicket">The replacement serialized ticket.</param>
    /// <param name="options">The replacement expiration, bounded by the original absolute deadline.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True only when the live matching entry was atomically replaced.</returns>
    /// <remarks>
    /// Existence, expiration, byte comparison and replacement must be one backend-atomic
    /// operation, ordered with Remove/RemoveAsync and all other updates, including those
    /// from independent clients/processes. Return false without writing when the key is
    /// absent, expired or changed. An unlocked Get followed by Set is not an implementation.
    /// Implementations must not retain or mutate the supplied byte arrays after completion.
    /// </remarks>
    Task<bool> TryRenewAsync(string key, byte[] expectedTicket, byte[] renewedTicket,
        DistributedCacheEntryOptions options, CancellationToken cancellationToken = default);
}
