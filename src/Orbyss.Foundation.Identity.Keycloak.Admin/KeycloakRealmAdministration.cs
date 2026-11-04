using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Updates typed realm settings without importing identities or replacing collections.</summary>
internal sealed class KeycloakRealmAdministration(KeycloakAdminTransport transport, IOptions<KeycloakAdminOptions> options)
    : IKeycloakRealmAdministration
{
    /// <summary>Uses provider web naming for the supported realm settings contract.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async ValueTask<KeycloakRealmSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        var representation = await transport.SendAsync(HttpMethod.Get, string.Empty, cancellationToken: cancellationToken);
        return representation?.Deserialize<KeycloakRealmSettings>(JsonOptions);
    }

    /// <inheritdoc />
    public async ValueTask CreateAsync(KeycloakRealmSettings settings, CancellationToken cancellationToken = default)
        => await CreateAsync(new KeycloakRealmInitialization { Settings = settings }, cancellationToken);

    /// <inheritdoc />
    public async ValueTask CreateAsync(KeycloakRealmInitialization initialization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(initialization.Settings);
        if (initialization.Clients.Any(client => client.Id is not null)
            || initialization.ClientScopes.Any(scope => scope.Id is not null || scope.ProtocolMappers?.Any(mapper => mapper.Id is not null) == true)
            || initialization.Roles.Any(role => role.Id is not null))
            throw new ArgumentException("Initial realm configuration must not contain provider-generated resource identifiers.", nameof(initialization));
        var representation = JsonSerializer.SerializeToElement(initialization.Settings, JsonOptions);
        var payload = representation.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        payload.Add("realm", options.Value.Realm);
        payload.Add("clients", initialization.Clients);
        payload.Add("clientScopes", initialization.ClientScopes);
        payload.Add("roles", new { realm = initialization.Roles });
        _ = await transport.SendRealmCollectionAsync(HttpMethod.Post, payload, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(KeycloakRealmSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Attributes is not null)
        {
            var current = await GetAsync(cancellationToken) ?? throw new InvalidOperationException("The configured realm is absent.");
            var attributes = current.Attributes is null ? new Dictionary<string, string>() : new Dictionary<string, string>(current.Attributes);
            foreach (var (key, value) in settings.Attributes) attributes[key] = value;
            settings = settings with { Attributes = attributes };
        }
        _ = await transport.SendAsync(HttpMethod.Put, string.Empty, settings, cancellationToken);
    }
}
