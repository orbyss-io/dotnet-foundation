using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Orbyss.Foundation.Authentication.BffCookie;
using Orbyss.Foundation.Authentication.Core;

[ShellFeature("CallbackProbe", DependsOn = [typeof(FoundationBffCookieFeature)])]
public sealed class CallbackProbeFeature : IWebShellFeature
{
    private static readonly IDistributedCache SharedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "deterministic-probe" };
    private static readonly ConcurrentDictionary<string, string> Codes = new(StringComparer.Ordinal);

    public void ConfigureServices(IServiceCollection services)
    {
        // A deployment can share one distributed backend across shell providers.
        services.AddSingleton(SharedCache);
        services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            var configuration = new OpenIdConnectConfiguration
            {
                Issuer = options.Authority,
                AuthorizationEndpoint = "https://identity.example/authorize",
                TokenEndpoint = "https://identity.example/token",
                UserInfoEndpoint = "https://identity.example/userinfo"
            };
            configuration.SigningKeys.Add(SigningKey);
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            options.Backchannel = new HttpClient(new FixtureBackchannel(options));
            options.Events.OnMessageReceived = context =>
            {
                if (context.Request.Query.ContainsKey("skip-handler")) context.SkipHandler();
                return Task.CompletedTask;
            };
        });
    }

    internal static string IssueCode(string issuer, string audience, string nonce, bool ambiguousSubject = false)
    {
        var code = Guid.NewGuid().ToString("N");
        var claims = new List<Claim> { new("sub", audience), new("nonce", nonce), new("name", audience),
            new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(AuthenticationClaimTypes.ValidatedIssuer, "token-supplied-untrusted-issuer"),
            new(AuthenticationClaimTypes.ValidatedSubject, "token-supplied-untrusted-subject") };
        if (ambiguousSubject) claims.Add(new Claim("sub", "another-account"));
        var token = new JwtSecurityToken(issuer, audience,
            claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256));
        Codes[code] = new JwtSecurityTokenHandler().WriteToken(token);
        return code;
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) { }

    private sealed class FixtureBackchannel(OpenIdConnectOptions options) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string json;
            if (request.RequestUri!.AbsolutePath == "/token")
            {
                var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                if (!Codes.TryRemove(form["code"].ToString(), out var token)) return new HttpResponseMessage(HttpStatusCode.BadRequest);
                json = JsonSerializer.Serialize(new { id_token = token, access_token = "fixture-access-token", token_type = "Bearer", expires_in = 300 });
            }
            else if (request.RequestUri.AbsolutePath == "/userinfo")
            {
                json = JsonSerializer.Serialize(new { sub = options.ClientId, name = options.ClientId });
            }
            else throw new InvalidOperationException("Unexpected fixture backchannel route.");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
