using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Identity.Admin;
using Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Exercises public standalone composition and typed realm administration.</summary>
internal static class StandaloneAdministrationProbe
{
    /// <summary>Verifies opt-in, options admission, ownership-preserving payloads and real redirect refusal.</summary>
    internal static async Task RunAsync()
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var services = new ServiceCollection();
        services.AddFoundationKeycloakAdministration(Configure,
            KeycloakAdminFeatures.Users | KeycloakAdminFeatures.Realms | KeycloakAdminFeatures.Access, "Development");
        services.RemoveAll<IHttpClientFactory>();
        services.AddSingleton<IHttpClientFactory>(new ProbeHttpClientFactory(client));
        await using var provider = services.BuildServiceProvider();
        Require(provider.GetService<IIdentityApplicationAdministration>() is null, "Standalone feature opt-in leaked client administration.");
        _ = await provider.GetRequiredService<IIdentityUserAdministration>().FindAsync("alice");
        var realms = provider.GetRequiredService<IKeycloakRealmAdministration>();
        Require((await realms.GetAsync())?.Enabled is true, "Configured realm projection failed.");
        await realms.CreateAsync(new KeycloakRealmSettings { Enabled = true, DisplayName = "Owned realm" });
        await realms.UpdateSettingsAsync(new KeycloakRealmSettings { LoginTheme = "owned", Attributes = new Dictionary<string, string> { ["release"] = "current" } });
        var roles = await provider.GetRequiredService<IIdentityEffectiveUserRoleReader>().GetEffectiveUserRolesAsync("subject/escaped");
        Require(roles.Single().Name == "subscriber" && roles.Single().Composite, "Effective role projection failed.");
        var update = handler.Requests.Single(request => request.Method == "PUT" && request.PathAndQuery.EndsWith("/admin/realms/tenant%20realm", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(update.Body!);
        Require(document.RootElement.GetProperty("attributes").GetProperty("operator-owned").GetString() == "preserved", "Realm update erased an unrelated attribute.");
        Require(!document.RootElement.TryGetProperty("users", out _) && !document.RootElement.TryGetProperty("clients", out _), "Realm settings update included identity collections.");
        Require(handler.Requests.Count(request => request.PathAndQuery.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal)) == 1, "Standalone clients did not share the token cache.");
        ValidateProductionAdmission();
        await RejectUntrustedResponsesAsync();
        await RejectStreamingResponsesAsync();
        await RejectRedirectAsync();
    }

    /// <summary>Selects fictional development-only administration credentials.</summary>
    private static void Configure(KeycloakAdminOptions options)
    {
        options.ServerUrl = "http://localhost:8080";
        options.Realm = "tenant realm";
        options.AdminRealm = "service realm";
        options.ClientId = "maintenance";
        options.ClientSecret = "fictional-secret";
        options.AllowHttpForLocalDevelopment = true;
    }

    /// <summary>Verifies standalone environment and capability selection rejection.</summary>
    private static void ValidateProductionAdmission()
    {
        var services = new ServiceCollection();
        services.AddFoundationKeycloakAdministration(Configure, KeycloakAdminFeatures.Users);
        using var provider = services.BuildServiceProvider();
        try
        {
            _ = provider.GetRequiredService<IOptions<KeycloakAdminOptions>>().Value;
            throw new InvalidOperationException("Production admitted HTTP administration.");
        }
        catch (OptionsValidationException) { }
        try
        {
            new ServiceCollection().AddFoundationKeycloakAdministration(Configure, KeycloakAdminFeatures.None);
            throw new InvalidOperationException("Empty administration selection was admitted.");
        }
        catch (ArgumentOutOfRangeException) { }
    }

    /// <summary>Rejects bounded malformed and oversized token responses without exposing fictional secrets.</summary>
    private static async Task RejectUntrustedResponsesAsync()
    {
        const string canary = "fictional-provider-secret-canary";
        foreach (var payload in new[] { "{\"access_token\":\"" + canary + "\",\"expires_in\":\"" + canary + "\"}",
            "{\"access_token\":\"" + canary, "[]", new string('x', 1_048_577) })
        {
            using var handler = new UntrustedTokenHandler(payload);
            using var client = new HttpClient(handler);
            var services = new ServiceCollection();
            services.AddFoundationKeycloakAdministration(Configure, KeycloakAdminFeatures.Users, "Development");
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(new ProbeHttpClientFactory(client));
            await using var provider = services.BuildServiceProvider();
            try
            {
                await provider.GetRequiredService<IIdentityUserAdministration>().FindAsync("alice");
                throw new InvalidOperationException("Invalid token response was admitted.");
            }
            catch (InvalidDataException error)
            {
                Require(!error.ToString().Contains(canary, StringComparison.Ordinal), "A provider response secret appeared in diagnostics.");
            }
        }
    }

    /// <summary>Proves unknown-length bodies are rejected early without automatic response buffering.</summary>
    private static async Task RejectStreamingResponsesAsync()
    {
        foreach (var creation in new[] { false, true })
        {
            using var handler = new StreamingAdminHandler(creation);
            using var client = new HttpClient(handler);
            var services = new ServiceCollection();
            services.AddFoundationKeycloakAdministration(Configure,
                KeycloakAdminFeatures.Users | KeycloakAdminFeatures.Applications, "Development");
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(new ProbeHttpClientFactory(client));
            await using var provider = services.BuildServiceProvider();
            try
            {
                if (creation) await provider.GetRequiredService<IIdentityApplicationAdministration>().CreateAsync(new("fictional-client"));
                else await provider.GetRequiredService<IIdentityUserAdministration>().FindAsync("alice");
                throw new InvalidOperationException("An oversized streaming response was admitted.");
            }
            catch (InvalidDataException) { }
            Require(!handler.Content.BufferingAttempted, "HttpClient buffered an oversized response before admission.");
            Require(handler.Content.Stream.BytesRead is > 1_048_576 and <= 1_056_768,
                "Streaming admission read beyond one bounded rejection chunk.");
        }
    }

    /// <summary>Exercises the default HTTP transport against an actual redirect trap.</summary>
    private static async Task RejectRedirectAsync()
    {
        using var destination = new TcpListener(IPAddress.Loopback, 0);
        destination.Start();
        using var source = new TcpListener(IPAddress.Loopback, 0);
        source.Start();
        var target = (IPEndPoint)destination.LocalEndpoint;
        var origin = (IPEndPoint)source.LocalEndpoint;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = RespondRedirectAsync(source, target.Port, deadline.Token);
        var services = new ServiceCollection();
        services.AddFoundationKeycloakAdministration(options =>
        {
            Configure(options);
            options.ServerUrl = "http://127.0.0.1:" + origin.Port;
        }, KeycloakAdminFeatures.Users, "Development");
        await using var provider = services.BuildServiceProvider();
        try
        {
            await provider.GetRequiredService<IIdentityUserAdministration>().FindAsync("alice", cancellationToken: deadline.Token);
            throw new InvalidOperationException("Administration followed a token redirect.");
        }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Found) { }
        await response;
        Require(!destination.Pending(), "A redirected endpoint received an administration request.");
    }

    /// <summary>Returns one fictional redirect without retaining request credentials.</summary>
    private static async Task RespondRedirectAsync(TcpListener source, int port, CancellationToken cancellationToken)
    {
        using var connection = await source.AcceptTcpClientAsync(cancellationToken);
        await using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken))) { }
        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{port}/capture\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, cancellationToken);
    }

    /// <summary>Fails the probe when a public composition guarantee is violated.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
