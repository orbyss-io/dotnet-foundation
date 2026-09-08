using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;

namespace Orbyss.Foundation.OpenApiExport;

/// <summary>Initializes the ASP.NET Core pipeline without opening a network listener.</summary>
internal sealed class NoopServer : IServer
{
    /// <summary>Gets the deliberately empty server feature collection.</summary>
    public IFeatureCollection Features { get; } = new FeatureCollection();

    /// <summary>Accepts the materialized request pipeline without binding a transport.</summary>
    public Task StartAsync<TContext>(
        IHttpApplication<TContext> application,
        CancellationToken cancellationToken) where TContext : notnull => Task.CompletedTask;

    /// <summary>Completes immediately because no transport was started.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Releases no resources because the server owns no transport.</summary>
    public void Dispose()
    {
    }
}
