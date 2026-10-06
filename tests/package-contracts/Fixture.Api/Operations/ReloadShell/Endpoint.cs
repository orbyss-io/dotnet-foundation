using CShells.Lifecycle;
using Foundation.ContractFixture.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.Json.AspNetCore;
namespace Foundation.ContractFixture.Api;
internal sealed class ReloadEndpoint(IShellRegistry registry, IJsonResponseFactory<ReloadResponse> responses)
{
    public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapPost("/reload/{shell}",
        (ReloadEndpoint endpoint, HttpContext context, string shell) => endpoint.ExecuteAsync(context, shell))
        .AllowAnonymous().WithJsonResponse<ReloadResponse>();
    public async Task<IResult> ExecuteAsync(HttpContext context, string shellName)
    {
        var previous = await registry.GetOrActivateAsync(shellName);
        await using var held = await previous.ServiceProvider.GetRequiredService<IFixtureStorageProbe>().HoldAsync(context.RequestAborted);
        var reload = await registry.ReloadAsync(shellName);
        if (reload.Error is not null || reload.NewShell is null || reload.Drain is null)
            throw new InvalidOperationException("Qualification shell reload failed.");
        var drain = reload.Drain.WaitAsync();
        await Task.Delay(100, context.RequestAborted);
        var blocked = !drain.IsCompleted;
        var alive = await held.CheckAliveAsync(context.RequestAborted);
        var oldSource = held.DataSourceId;
        await held.DisposeAsync();
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
        var replacement = await reload.NewShell.ServiceProvider.GetRequiredService<IFixtureStorageProbe>().ObserveAsync(context.RequestAborted);
        return responses.Create(new(oldSource, replacement.DataSourceId, blocked, alive, drain.IsCompletedSuccessfully));
    }
}
