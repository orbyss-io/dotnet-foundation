using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using CShells;
using System.Security.Cryptography;
using System.Text;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Stores full authentication tickets outside the browser cookie.</summary>
internal sealed class DistributedTicketStore(
    IDistributedCache cache,
    IOptions<FoundationWebOptions> webOptions,
    ShellSettings settings) : ITicketStore
{
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
        var absolute = DateTimeOffset.UtcNow.AddMinutes(webOptions.Value.SessionAbsoluteMinutes);
        ticket.Properties.Items[AbsoluteExpiryProperty] =
            absolute.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        await RenewAsync(key, ticket).ConfigureAwait(false);
        return key;
    }

    /// <inheritdoc />
    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (!IsOwned(key)) throw new InvalidOperationException("A session ticket cannot be renewed outside its owning shell.");
        var idleExpiry = ticket.Properties.ExpiresUtc
            ?? DateTimeOffset.UtcNow.AddMinutes(webOptions.Value.SessionIdleMinutes);
        var absoluteExpiry = ReadAbsoluteExpiry(ticket) ?? DateTimeOffset.UtcNow;
        var expires = idleExpiry < absoluteExpiry ? idleExpiry : absoluteExpiry;
        return cache.SetAsync(
            key,
            TicketSerializer.Default.Serialize(ticket),
            new DistributedCacheEntryOptions { AbsoluteExpiration = expires });
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!IsOwned(key)) return null;
        var bytes = await cache.GetAsync(key).ConfigureAwait(false);
        return bytes is null ? null : TicketSerializer.Default.Deserialize(bytes);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key) => IsOwned(key) ? cache.RemoveAsync(key) : Task.CompletedTask;

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
