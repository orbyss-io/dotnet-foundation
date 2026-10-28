using System.Reflection;
using System.Security.Claims;
using CShells;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.BffCookie;

internal static class TicketCacheContractProbe
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        var control = provider.GetRequiredService<TicketRenewalControl>();
        control.Reset();
        var store = provider.GetRequiredService<TicketRenewalObservedStore>().Native;
        var type = store.GetType();
        var cache = provider.GetRequiredService<IDistributedCache>();
        var options = provider.GetRequiredService<IOptions<FoundationWebOptions>>();
        var settings = provider.GetRequiredService<ShellSettings>();
        ITicketStore Store(IDistributedCache backend) => (ITicketStore)Activator.CreateInstance(type, backend, options, settings)!;

        var key = await store.StoreAsync(Ticket());
        var ticket = await store.RetrieveAsync(key) ?? throw new Exception("Missing direct-store ticket");
        var cap = ticket.Properties.Items[TicketRenewalControl.Cap];
        ticket.Properties.Items[TicketRenewalControl.Cap] = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds().ToString();
        await store.RenewAsync(key, ticket);
        Require((await store.RetrieveAsync(key))!.Properties.Items[TicketRenewalControl.Cap] == cap, "Renewal replaced the server-owned cap");
        await store.RemoveAsync(key);
        await store.RenewAsync(key, ticket);
        Require(await store.RetrieveAsync(key) is null, "Direct renewal recreated removed key");
        var missingKey = key[..key.LastIndexOf(':')] + ":" + Guid.NewGuid().ToString("N");
        await store.RenewAsync(missingKey, ticket);
        Require(await store.RetrieveAsync(missingKey) is null, "Direct renewal created nonexistent key");

        // Block the actual memory cache's clock while renewal holds its native gate.
        // A second native store on the same cache must not remove/read through it.
        key = await store.StoreAsync(Ticket());
        ticket = (await store.RetrieveAsync(key))!;
        var secondStore = Store(cache);
        control.Clock.BlockNextRead();
        var renewing = Task.Run(() => store.RenewAsync(key, ticket));
        try
        {
            await control.Clock.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var removing = secondStore.RemoveAsync(key);
            var reading = secondStore.RetrieveAsync(key);
            Require(!removing.IsCompleted && !reading.IsCompleted, "Independent memory store bypassed shared synchronization");
            control.Clock.ContinueRead();
            await Task.WhenAll(renewing, removing, reading).WaitAsync(TimeSpan.FromSeconds(5));
            Require(await store.RetrieveAsync(key) is null, "Serialized removal did not win over renewal");
        }
        finally { control.Clock.ContinueRead(); await renewing.WaitAsync(TimeSpan.FromSeconds(5)); }

        // Unknown cache implementations must fail rather than silently use unsafe upserts.
        using var conditional = new ConditionalProbeCache();
        try { Store(new PlainProbeCache(conditional)); throw new Exception("Unsafe cache was admitted"); }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException) { }
        var remoteA = Store(conditional);
        var remoteB = Store(conditional);
        key = await remoteA.StoreAsync(Ticket());
        ticket = (await remoteA.RetrieveAsync(key))!;
        ticket.Properties.Items["synthetic-update"] = "new";
        await remoteA.RenewAsync(key, ticket);
        Require((await remoteB.RetrieveAsync(key))!.Properties.Items["synthetic-update"] == "new", "Conditional cache lost valid renewal");

        // Pause after the native read but before the provider's atomic comparison.
        conditional.Arm();
        var late = remoteA.RenewAsync(key, ticket);
        await conditional.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await remoteB.RemoveAsync(key);
        conditional.Release.TrySetResult();
        await late.WaitAsync(TimeSpan.FromSeconds(5));
        Require(await remoteB.RetrieveAsync(key) is null && conditional.Rejected == 1,
            "Backend comparison recreated a key removed by an independent store");

        // A changed live value also loses the comparison without overwriting the winner.
        key = await remoteA.StoreAsync(Ticket());
        ticket = (await remoteA.RetrieveAsync(key))!;
        conditional.Arm();
        late = remoteA.RenewAsync(key, ticket);
        await conditional.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var winner = (await remoteB.RetrieveAsync(key))!;
        winner.Properties.Items["winner"] = "preserved";
        await conditional.SetAsync(key, TicketSerializer.Default.Serialize(winner),
            new DistributedCacheEntryOptions { AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(1) });
        conditional.Release.TrySetResult();
        await late.WaitAsync(TimeSpan.FromSeconds(5));
        Require((await remoteB.RetrieveAsync(key))!.Properties.Items["winner"] == "preserved" && conditional.Rejected == 2,
            "Conditional renewal overwrote a competing cache value");
        Console.WriteLine("Native direct renewal, immutable cap, shared-memory synchronization, unsafe-cache rejection and conditional-provider races passed.");
    }

    private static AuthenticationTicket Ticket() => new(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim("synthetic", "owner")], "fixture")), new AuthenticationProperties {
            IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1) }, "Cookies");
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}

// Contract fixture only: real memory cache plus an atomic conditional operation.
// This does not qualify Redis or any other network/backend implementation.
internal sealed class ConditionalProbeCache : IBffSessionTicketCache, IDisposable
{
    private readonly MemoryDistributedCache cache = new(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool armed;
    public int Rejected { get; private set; }
    public TaskCompletionSource Entered { get; private set; } = New();
    public TaskCompletionSource Release { get; private set; } = New();
    public void Arm() { Entered = New(); Release = New(); armed = true; }
    public async Task<bool> TryRenewAsync(string key, byte[] expectedTicket, byte[] renewedTicket,
        DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        if (armed) { armed = false; Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await cache.GetAsync(key, cancellationToken);
            if (current is null || !current.AsSpan().SequenceEqual(expectedTicket)) { Rejected++; return false; }
            await cache.SetAsync(key, renewedTicket, options, cancellationToken);
            return true;
        }
        finally { gate.Release(); }
    }
    public byte[]? Get(string key) { gate.Wait(); try { return cache.Get(key); } finally { gate.Release(); } }
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    { await gate.WaitAsync(token); try { return await cache.GetAsync(key, token); } finally { gate.Release(); } }
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    { gate.Wait(); try { cache.Set(key, value, options); } finally { gate.Release(); } }
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    { await gate.WaitAsync(token); try { await cache.SetAsync(key, value, options, token); } finally { gate.Release(); } }
    public void Remove(string key) { gate.Wait(); try { cache.Remove(key); } finally { gate.Release(); } }
    public async Task RemoveAsync(string key, CancellationToken token = default)
    { await gate.WaitAsync(token); try { await cache.RemoveAsync(key, token); } finally { gate.Release(); } }
    public void Refresh(string key) { gate.Wait(); try { cache.Refresh(key); } finally { gate.Release(); } }
    public async Task RefreshAsync(string key, CancellationToken token = default)
    { await gate.WaitAsync(token); try { await cache.RefreshAsync(key, token); } finally { gate.Release(); } }
    public void Dispose() => gate.Dispose();
    private static TaskCompletionSource New() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class PlainProbeCache(IDistributedCache cache) : IDistributedCache
{
    public byte[]? Get(string key) => cache.Get(key);
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => cache.GetAsync(key, token);
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => cache.Set(key, value, options);
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => cache.SetAsync(key, value, options, token);
    public void Remove(string key) => cache.Remove(key);
    public Task RemoveAsync(string key, CancellationToken token = default) => cache.RemoveAsync(key, token);
    public void Refresh(string key) => cache.Refresh(key);
    public Task RefreshAsync(string key, CancellationToken token = default) => cache.RefreshAsync(key, token);
}
