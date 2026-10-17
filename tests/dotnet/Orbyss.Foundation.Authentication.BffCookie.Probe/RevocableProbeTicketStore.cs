using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

/// <summary>Delegates every operation to the actual native store; an explicit synthetic admin operation revokes only observed fixture tickets.</summary>
internal sealed class RevocableProbeTicketStore(ITicketStore native) : ITicketStore
{
    private readonly ConcurrentDictionary<string, byte> keys = new(StringComparer.Ordinal);
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = await native.StoreAsync(ticket);
        keys.TryAdd(key, 0);
        return key;
    }
    public Task RenewAsync(string key, AuthenticationTicket ticket) => native.RenewAsync(key, ticket);
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => native.RetrieveAsync(key);
    public Task RemoveAsync(string key) { keys.TryRemove(key, out _); return native.RemoveAsync(key); }
    public async Task RevokeObservedAsync()
    {
        foreach (var key in keys.Keys) await RemoveAsync(key);
    }
}
