using System.Text;
using CShells.Lifecycle.Blueprints;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.BffCookie;
using Orbyss.Foundation.WebDefaults;

internal static class OptionsBindingProbe
{
    public static void Run()
    {
        using var first = Provider("\"Scopes\":[\"openid\",\"profile\",\"offline_access\",\"program-kit-api\"]");
        var expected = new[] { "openid", "profile", "offline_access", "program-kit-api" };
        CheckScopes(first, expected);
        using var second = Provider("\"Scopes\":[\"openid\",\"other-api\"]");
        CheckScopes(second, ["openid", "other-api"]);
        CheckScopes(first, expected);
        using var omitted = Provider("");
        CheckScopes(omitted, ["openid", "profile", "offline_access", "orbyss-foundation-api"]);
        using var minimal = Provider("\"Scopes\":[\"openid\"]");
        CheckScopes(minimal, ["openid"]);
        foreach (var selection in new[] { "[]", "null", "\"\"", "\"openid\"", "[\"profile\"]", "[\"openid\",\"\"]", "[\"openid\",null]", "[\"openid\",\"two scopes\"]", "[\"openid\",\"bad\\\\scope\"]", "[\"openid\",\"é\"]", "[\"openid\",\"openid\"]", "{\"unexpected\":\"openid\"}", "{\"1\":\"openid\"}", "[\"openid\",{\"nested\":\"scope\"}]" })
        {
            using var invalid = Provider("\"Scopes\":" + selection);
            try
            {
                _ = invalid.GetRequiredService<IOptions<FoundationWebOptions>>().Value;
                throw new Exception("Invalid scope selection accepted: " + selection);
            }
            catch (OptionsValidationException) { }
        }
        using var security = Provider("""
            "Scopes":["openid"],"AllowedOrigins":["https://consumer.example"],
            "RolePermissions":{"reader":["records.read"]},"ScopePermissions":{"records":["records.read"]},
            "ResponsePolicies":{"Policies":{"default":{"ReferrerPolicy":"same-origin"}}}
            """);
        var web = security.GetRequiredService<IOptions<FoundationWebOptions>>().Value;
        Require(web.AllowedOrigins.SequenceEqual(["https://consumer.example"]), "origins accumulated defaults");
        Require(web.RolePermissions.Count == 1 && web.RolePermissions["reader"].SequenceEqual(["records.read"]), "role permissions accumulated defaults");
        Require(web.ScopePermissions.Count == 1 && web.ScopePermissions["records"].SequenceEqual(["records.read"]), "scope permissions accumulated defaults");
        var policies = security.GetRequiredService<IOptions<WebResponsePoliciesOptions>>().Value;
        Require(policies.Policies.Count == 3 && policies.Policies["default"].ReferrerPolicy == "same-origin"
            && policies.Policies["default"].ContentSecurityPolicy.Contains("frame-ancestors 'none'", StringComparison.Ordinal)
            && policies.Policies["private"].PublicAssetMaxAgeSeconds == 0, "named policy defaults/override changed");
        using var locales = Provider("\"DefaultLocale\":\"nl\",\"SupportedLocales\":[\"nl\",\"de\"]");
        var localization = locales.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
        Require(localization.SupportedCultures!.Select(c => c.Name).SequenceEqual(["nl", "de"]), "locale defaults leaked into explicit selection");
        using var defaultLocales = Provider("");
        Require(defaultLocales.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value.SupportedCultures!.Select(c => c.Name).SequenceEqual(["en"]), "omitted locale fallback changed");
        Require(locales.GetRequiredService<IOptionsFactory<RequestLocalizationOptions>>().Create(Options.DefaultName)
            .SupportedCultures!.Select(c => c.Name).SequenceEqual(["nl", "de"]), "independent shell locales leaked during recreation");
        foreach (var selection in new[] { "[]", "null", "\"en\"", "[\"\"]", "[null]", "[\"nl\"]", "[\"en\",\"en\"]", "{\"unexpected\":\"en\"}" })
        {
            using var invalid = Provider("\"SupportedLocales\":" + selection);
            try
            {
                _ = invalid.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
                throw new Exception("Invalid locale selection accepted: " + selection);
            }
            catch (OptionsValidationException) { }
        }
        foreach (var selection in new[]
        {
            "\"CallbackPath\":null", "\"CallbackPath\":\"//other.example/callback\"",
            "\"CallbackPath\":\"/oidc/{value}\"", "\"CallbackPath\":\"/oidc/callback?x=1\"",
            "\"CallbackPath\":\"/oidc/../callback\"", "\"CallbackPath\":\"/signin-oidc/\"",
            "\"CallbackPath\":\"/bff/logout\"", "\"CallbackPath\":\"/signout-oidc\"",
            "\"AccessDeniedPath\":\"/SIGNIN-OIDC\""
        })
        {
            using var invalid = Provider(selection);
            try
            {
                _ = invalid.GetRequiredService<IOptions<FoundationWebOptions>>().Value;
                throw new Exception("Invalid protocol route accepted: " + selection);
            }
            catch (OptionsValidationException) { }
        }
        Console.WriteLine("JSON -> shell configuration -> registered options -> OIDC scopes, locales and protocol paths passed.");
    }

    private static void CheckScopes(ServiceProvider provider, string[] expected)
    {
        var rebound = new FoundationWebOptions();
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var configure in provider.GetServices<IConfigureOptions<FoundationWebOptions>>())
                configure.Configure(rebound);
            Require(rebound.Scopes.SequenceEqual(expected), "reapplied registered binding accumulated scopes");
            var oidc = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
            Require(oidc.Scope.SequenceEqual(expected), "effective OIDC scopes changed: " + string.Join(" ", oidc.Scope));
            var bound = provider.GetRequiredService<IOptions<FoundationWebOptions>>().Value;
            Require(bound.Scopes.SequenceEqual(expected), "configured scopes changed: " + string.Join(" ", bound.Scopes));
            var fresh = provider.GetRequiredService<IOptionsFactory<FoundationWebOptions>>().Create(Options.DefaultName);
            Require(fresh.Scopes.SequenceEqual(expected), "fresh options accumulated scopes");
            Require(oidc.UsePkce && oidc.SaveTokens && oidc.ResponseType == "code", "OIDC protections changed");
            Require(oidc.TokenValidationParameters.ValidateIssuer && oidc.TokenValidationParameters.ValidateAudience
                && oidc.TokenValidationParameters.ValidateIssuerSigningKey && oidc.TokenValidationParameters.ValidateLifetime, "token validation weakened");
            provider.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>().Clear();
        }
    }

    private static ServiceProvider Provider(string selection)
    {
        var json = "{\"Configuration\":{\"Foundation\":{\"Web\":{\"Authority\":\"https://identity.example/realms/probe\",\"ClientId\":\"probe-bff\",\"ClientSecret\":\"probe-only\",\"Audience\":\"probe-api\""
            + (selection.Length == 0 ? "" : "," + selection) + "}}}}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var settings = new ConfigurationShellBlueprint(Guid.NewGuid().ToString("N"), configuration)
            .ComposeAsync().GetAwaiter().GetResult();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new ProbeEnvironment(Environments.Production));
        new FoundationAuthenticationFeature(settings).ConfigureServices(services);
        new FoundationBffCookieFeature().ConfigureServices(services);
        new FoundationWebDefaultsFeature(settings).ConfigureServices(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
