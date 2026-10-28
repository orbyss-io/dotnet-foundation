using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using CShells;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Stores full authentication tickets outside the browser cookie.</summary>
internal sealed class DistributedTicketStore(
    IDistributedCache cache,
    IOptions<FoundationWebOptions> webOptions,
    ShellSettings settings) : ITicketStore
{
    /// <summary>Shares bounded synchronization across stores using the same in-process cache.</summary>
    private static readonly ConditionalWeakTable<IDistributedCache, SemaphoreSlim> MemoryGates = new();

    /// <summary>Remote caches must supply backend-atomic replacement rather than a process-local lock.</summary>
    private readonly SemaphoreSlim? cacheGate = cache.GetType() == typeof(MemoryDistributedCache)
        ? MemoryGates.GetValue(cache, static _ => new SemaphoreSlim(1, 1))
        : cache is IBffSessionTicketCache ? null
        : throw new InvalidOperationException(
            "The BFF ticket store requires MemoryDistributedCache or an IBffSessionTicketCache with backend-atomic conditional renewal.");

    /// <summary>Namespaces opaque session keys in the distributed cache.</summary>
    private const string KeyPrefix = "orbyss-foundation:session:";

    /// <summary>Isolates each owning shell's keys on a shared cache without delimiter ambiguity.</summary>
    private readonly string ownedKeyPrefix = KeyPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(settings.Id.ToString()))) + ":";

    /// <summary>Stores the non-extendable session deadline in the protected ticket.</summary>
    private const string AbsoluteExpiryProperty = ".orbyss-foundation.absolute-expiry";

    /// <inheritdoc />
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = $"{ownedKeyPrefix}{Guid.NewGuid():N}";
        var absolute = DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.AddMinutes(webOptions.Value.SessionAbsoluteMinutes).ToUnixTimeSeconds());
        ticket.Properties.Items[AbsoluteExpiryProperty] =
            absolute.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        // Only creation may introduce a key. Renewal must never act as an upsert.
        if (cacheGate is not null) await cacheGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await cache.SetAsync(key, TicketSerializer.Default.Serialize(ticket),
                new DistributedCacheEntryOptions { AbsoluteExpiration = Expiration(ticket, absolute) }).ConfigureAwait(false);
        }
        finally { cacheGate?.Release(); }
        return key;
    }

    /// <inheritdoc />
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (!IsOwned(key)) throw new InvalidOperationException("A session ticket cannot be renewed outside its owning shell.");
        if (cacheGate is not null) await cacheGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var previous = await cache.GetAsync(key).ConfigureAwait(false);
            if (previous is null) return;
            var current = TicketSerializer.Default.Deserialize(previous);
            var now = DateTimeOffset.UtcNow;
            var absolute = current is null ? null : ReadAbsoluteExpiry(current);
            if (absolute is null || absolute <= now || current!.Properties.ExpiresUtc <= now) return;

            // The current server entry owns the original cap, even if a caller changed its copy.
            ticket.Properties.Items[AbsoluteExpiryProperty] = current.Properties.Items[AbsoluteExpiryProperty];
            var expires = Expiration(ticket, absolute.Value);
            if (expires <= now) return;
            var renewed = TicketSerializer.Default.Serialize(ticket);
            var options = new DistributedCacheEntryOptions { AbsoluteExpiration = expires };
            if (cacheGate is not null)
                await cache.SetAsync(key, renewed, options).ConfigureAwait(false);
            else
                // No retry/upsert if logout, expiry or another renewal won the comparison.
                _ = await ((IBffSessionTicketCache)cache).TryRenewAsync(key, previous, renewed, options).ConfigureAwait(false);
        }
        finally { cacheGate?.Release(); }
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!IsOwned(key)) return null;
        if (cacheGate is not null) await cacheGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var bytes = await cache.GetAsync(key).ConfigureAwait(false);
            return bytes is null ? null : TicketSerializer.Default.Deserialize(bytes);
        }
        finally { cacheGate?.Release(); }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        if (!IsOwned(key)) return;
        if (cacheGate is not null) await cacheGate.WaitAsync().ConfigureAwait(false);
        try { await cache.RemoveAsync(key).ConfigureAwait(false); }
        finally { cacheGate?.Release(); }
    }

    /// <summary>Bounds idle access by the server-owned original absolute deadline.</summary>
    private DateTimeOffset Expiration(AuthenticationTicket ticket, DateTimeOffset absolute)
    {
        var idle = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddMinutes(webOptions.Value.SessionIdleMinutes);
        return idle < absolute ? idle : absolute;
    }

    /// <summary>Rejects foreign and historical unbound keys before touching a shared cache.</summary>
    private bool IsOwned(string key) => key.StartsWith(ownedKeyPrefix, StringComparison.Ordinal);

    /// <summary>Reads the non-extendable session deadline stored when the ticket was created.</summary>
    private static DateTimeOffset? ReadAbsoluteExpiry(AuthenticationTicket ticket)
    {
        if (!ticket.Properties.Items.TryGetValue(AbsoluteExpiryProperty, out var value)
            || !long.TryParse(
                value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var seconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }
}
