using System.Text.Json;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Implements typed partial provider configuration over the shared authenticated transport.</summary>
internal sealed class KeycloakConfigurationAdministration(KeycloakAdminTransport transport) : IKeycloakConfigurationAdministration
{
    /// <summary>Uses provider web naming while omitting explicitly nullable configuration fields.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KeycloakClientConfiguration>> GetClientsAsync(string? clientId = null, int first = 0, int maximum = 100, CancellationToken cancellationToken = default)
    {
        if (first < 0 || maximum is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maximum));
        var query = $"clients?first={first}&max={maximum}&viewableOnly=true";
        if (clientId is not null) query += "&clientId=" + Segment(clientId);
        return GetListAsync<KeycloakClientConfiguration>(query, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<KeycloakClientConfiguration?> GetClientAsync(string clientId, CancellationToken cancellationToken = default) =>
        GetAsync<KeycloakClientConfiguration>("clients/" + Segment(clientId), cancellationToken);

    /// <inheritdoc />
    public ValueTask<string> CreateClientAsync(KeycloakClientConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.ClientId);
        return transport.CreateAsync("clients", Payload(configuration), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateClientAsync(string clientId, KeycloakClientConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.DefaultClientScopes is not null || configuration.OptionalClientScopes is not null)
            throw new ArgumentException("Use scope assignment operations to update client scopes.", nameof(configuration));
        if (configuration.Attributes is not null || configuration.AuthenticationFlowBindingOverrides is not null)
        {
            var current = await GetClientAsync(clientId, cancellationToken) ?? throw new InvalidOperationException("The configured client is absent.");
            configuration = configuration with { Attributes = Merge(current.Attributes, configuration.Attributes),
                AuthenticationFlowBindingOverrides = Merge(current.AuthenticationFlowBindingOverrides, configuration.AuthenticationFlowBindingOverrides) };
        }
        _ = await transport.SendAsync(HttpMethod.Put, "clients/" + Segment(clientId), Payload(configuration), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KeycloakScopeConfiguration>> GetScopesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<KeycloakScopeConfiguration>("client-scopes", cancellationToken);

    /// <inheritdoc />
    public ValueTask<string> CreateScopeAsync(KeycloakScopeConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.Name);
        return transport.CreateAsync("client-scopes", Payload(configuration), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateScopeAsync(string scopeId, KeycloakScopeConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.ProtocolMappers is not null) throw new ArgumentException("Use mapper operations to update scope mappings.", nameof(configuration));
        var path = "client-scopes/" + Segment(scopeId);
        if (configuration.Attributes is not null)
        {
            var current = await GetAsync<KeycloakScopeConfiguration>(path, cancellationToken) ?? throw new InvalidOperationException("The configured scope is absent.");
            configuration = configuration with { Attributes = Merge(current.Attributes, configuration.Attributes) };
        }
        _ = await transport.SendAsync(HttpMethod.Put, path, Payload(configuration), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KeycloakProtocolMapperConfiguration>> GetProtocolMappersAsync(string scopeId, CancellationToken cancellationToken = default) =>
        GetListAsync<KeycloakProtocolMapperConfiguration>("client-scopes/" + Segment(scopeId) + "/protocol-mappers/models", cancellationToken);

    /// <inheritdoc />
    public ValueTask<string> CreateProtocolMapperAsync(string scopeId, KeycloakProtocolMapperConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.ProtocolMapper);
        return transport.CreateAsync("client-scopes/" + Segment(scopeId) + "/protocol-mappers/models", Payload(configuration), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateProtocolMapperAsync(string scopeId, string mapperId, KeycloakProtocolMapperConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var path = "client-scopes/" + Segment(scopeId) + "/protocol-mappers/models/" + Segment(mapperId);
        if (configuration.Config is not null)
        {
            var current = await GetAsync<KeycloakProtocolMapperConfiguration>(path, cancellationToken) ?? throw new InvalidOperationException("The configured mapper is absent.");
            configuration = configuration with { Config = Merge(current.Config, configuration.Config) };
        }
        _ = await transport.SendAsync(HttpMethod.Put, path, Payload(configuration), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KeycloakScopeConfiguration>> GetClientScopesAsync(string clientId, bool optional, CancellationToken cancellationToken = default) =>
        GetListAsync<KeycloakScopeConfiguration>("clients/" + Segment(clientId) + (optional ? "/optional-client-scopes" : "/default-client-scopes"), cancellationToken);

    /// <inheritdoc />
    public async ValueTask AddClientScopeAsync(string clientId, string scopeId, bool optional, CancellationToken cancellationToken = default) =>
        _ = await transport.SendAsync(HttpMethod.Put, "clients/" + Segment(clientId) + (optional ? "/optional-client-scopes/" : "/default-client-scopes/")
            + Segment(scopeId), cancellationToken: cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KeycloakRoleConfiguration>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<KeycloakRoleConfiguration>("roles?briefRepresentation=false", cancellationToken);

    /// <inheritdoc />
    public async ValueTask CreateRoleAsync(KeycloakRoleConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.Name);
        _ = await transport.SendAsync(HttpMethod.Post, "roles", Payload(configuration), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateRoleAsync(string roleName, KeycloakRoleConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var path = "roles/" + Segment(roleName);
        if (configuration.Attributes is not null)
        {
            var current = await GetAsync<KeycloakRoleConfiguration>(path, cancellationToken) ?? throw new InvalidOperationException("The configured role is absent.");
            configuration = configuration with { Attributes = Merge(current.Attributes, configuration.Attributes) };
        }
        _ = await transport.SendAsync(HttpMethod.Put, path, Payload(configuration), cancellationToken);
    }

    /// <summary>Reads one optional typed provider representation.</summary>
    private async ValueTask<T?> GetAsync<T>(string path, CancellationToken cancellationToken) where T : class =>
        (await transport.SendAsync(HttpMethod.Get, path, cancellationToken: cancellationToken))?.Deserialize<T>(JsonOptions);

    /// <summary>Reads a typed provider list and rejects malformed response shapes.</summary>
    private async ValueTask<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        var value = await transport.SendAsync(HttpMethod.Get, path, cancellationToken: cancellationToken);
        if (value is not { ValueKind: JsonValueKind.Array } list) throw new InvalidDataException("Keycloak configuration list was absent or malformed.");
        return list.Deserialize<T[]>(JsonOptions) ?? [];
    }

    /// <summary>Serializes declared fields while keeping provider-generated resource IDs out of mutations.</summary>
    private static Dictionary<string, JsonElement> Payload<T>(T configuration) =>
        JsonSerializer.SerializeToElement(configuration, JsonOptions).EnumerateObject()
            .Where(property => property.Name != "id").ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);

    /// <summary>Merges only explicitly supplied map keys and preserves omitted maps entirely.</summary>
    private static IReadOnlyDictionary<string, T>? Merge<T>(IReadOnlyDictionary<string, T>? existing, IReadOnlyDictionary<string, T>? desired)
    {
        if (desired is null) return null;
        var result = existing is null ? new Dictionary<string, T>(StringComparer.Ordinal) : new Dictionary<string, T>(existing, StringComparer.Ordinal);
        foreach (var (key, value) in desired) result[key] = value;
        return result;
    }

    /// <summary>Requires and escapes one provider resource identifier.</summary>
    private static string Segment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return KeycloakAdminTransport.Escape(value);
    }
}
