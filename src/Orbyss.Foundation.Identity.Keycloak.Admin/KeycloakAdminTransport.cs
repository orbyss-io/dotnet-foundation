using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Sends authenticated, bounded requests to one Keycloak realm's Admin REST API.</summary>
internal sealed class KeycloakAdminTransport(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakAdminOptions> options,
    TimeProvider timeProvider) : IDisposable
{
    /// <summary>Names the isolated HTTP client used for Keycloak administration.</summary>
    internal const string HttpClientName = "Orbyss.Foundation.Identity.Keycloak.Admin";
    /// <summary>Applies interoperable web JSON naming and parsing conventions.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>Coalesces concurrent service-account token refreshes.</summary>
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    /// <summary>Holds the current safely bounded service-account access token.</summary>
    private AccessToken? token;

    /// <summary>Sends an authenticated Admin REST request and returns its optional JSON representation.</summary>
    public async ValueTask<JsonElement?> SendAsync(
        HttpMethod method,
        string relativePath,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        var configured = options.Value;
        var suffix = string.IsNullOrEmpty(relativePath) ? string.Empty : "/" + relativePath.TrimStart('/');
        var uri = $"{configured.ServerUrl.TrimEnd('/')}/admin/realms/{Escape(configured.Realm)}{suffix}";
        return await SendToAsync(method, uri, body, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates only the configured target realm through the realm collection endpoint.</summary>
    internal ValueTask<JsonElement?> SendRealmCollectionAsync(HttpMethod method, object body, CancellationToken cancellationToken) =>
        SendToAsync(method, options.Value.ServerUrl.TrimEnd('/') + "/admin/realms", body, cancellationToken);

    /// <summary>Creates a resource and extracts its stable identifier from the response location.</summary>
    public async ValueTask<string> CreateAsync(
        string relativePath,
        object body,
        CancellationToken cancellationToken = default)
    {
        var configured = options.Value;
        var uri = $"{configured.ServerUrl.TrimEnd('/')}/admin/realms/{Escape(configured.Realm)}/{relativePath.TrimStart('/')}";
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken).ConfigureAwait(false));
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configured.TimeoutSeconds));
        using var response = await httpClientFactory.CreateClient(HttpClientName)
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Keycloak Admin REST creation returned HTTP {(int)response.StatusCode}.", null, response.StatusCode);
        }
        await DrainAsync(response, timeout.Token).ConfigureAwait(false);
        var location = response.Headers.Location?.Segments.LastOrDefault()?.Trim('/');
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new InvalidDataException($"Keycloak Admin REST POST {relativePath} did not return a resource Location.");
        }
        return Uri.UnescapeDataString(location);
    }

    /// <summary>Disposes the token-refresh synchronization primitive.</summary>
    public void Dispose() => tokenLock.Dispose();

    /// <summary>Escapes one untrusted Admin REST path or query value.</summary>
    internal static string Escape(string value) => Uri.EscapeDataString(value);

    /// <summary>Executes one internally selected endpoint without exposing a raw transport API.</summary>
    private async ValueTask<JsonElement?> SendToAsync(HttpMethod method, string uri, object? body, CancellationToken cancellationToken)
    {
        var configured = options.Value;
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken).ConfigureAwait(false));
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configured.TimeoutSeconds));
        using var response = await httpClientFactory.CreateClient(HttpClientName)
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound && method == HttpMethod.Get)
        {
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Keycloak Admin REST operation returned HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }
        if (response.Content.Headers.ContentLength == 0 || response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }
        return await ReadJsonAsync(response, timeout.Token).ConfigureAwait(false);
    }

    /// <summary>Gets a safely cached service-account token or acquires a new one.</summary>
    private async ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var configured = options.Value;
        if (token is { } cached && cached.ExpiresAt > timeProvider.GetUtcNow().AddSeconds(configured.RefreshBeforeExpirySeconds))
        {
            return cached.Value;
        }
        await tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (token is { } locked && locked.ExpiresAt > timeProvider.GetUtcNow().AddSeconds(configured.RefreshBeforeExpirySeconds))
            {
                return locked.Value;
            }
            var endpoint = $"{configured.ServerUrl.TrimEnd('/')}/realms/{Escape(configured.AdminRealm)}/protocol/openid-connect/token";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            var encoded = $"{FormEncode(configured.ClientId)}:{FormEncode(configured.ClientSecret)}";
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(encoded)));
            request.Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")]);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(configured.TimeoutSeconds));
            using var response = await httpClientFactory.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Keycloak service-account token endpoint returned HTTP {(int)response.StatusCode}.", null, response.StatusCode);
            }
            var root = await ReadJsonAsync(response, timeout.Token).ConfigureAwait(false);
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("access_token", out var tokenValue) || tokenValue.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("expires_in", out var expiry))
                throw new InvalidDataException("The Keycloak token response did not contain a valid access_token and expires_in.");
            var value = tokenValue.GetString();
            var expires = expiry.ValueKind switch
            {
                JsonValueKind.Number when expiry.TryGetInt32(out var numeric) => numeric,
                JsonValueKind.String when int.TryParse(expiry.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0
            };
            if (string.IsNullOrWhiteSpace(value) || expires <= 0)
            {
                throw new InvalidDataException("The Keycloak token response did not contain a valid access_token and expires_in.");
            }
            token = new(value, timeProvider.GetUtcNow().AddSeconds(expires));
            return value;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    /// <summary>Applies OAuth form encoding to one client credential.</summary>
    private static string FormEncode(string value) => Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);

    /// <summary>Drains an unneeded creation body within the operation deadline and fixed response bound.</summary>
    private static async ValueTask DrainAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        const int maximumBytes = 1_048_576;
        if (response.Content.Headers.ContentLength > maximumBytes)
            throw new InvalidDataException("Keycloak administration response exceeded its size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[8192];
        var total = 0;
        while (true)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            total += count;
            if (total > maximumBytes) throw new InvalidDataException("Keycloak administration response exceeded its size limit.");
        }
    }

    /// <summary>Bounds provider responses and omits response contents from parse diagnostics.</summary>
    private static async ValueTask<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        const int maximumBytes = 1_048_576;
        if (response.Content.Headers.ContentLength > maximumBytes)
            throw new InvalidDataException("Keycloak administration response exceeded its size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (buffer.Length + count > maximumBytes)
                throw new InvalidDataException("Keycloak administration response exceeded its size limit.");
            buffer.Write(bytes, 0, count);
        }
        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Keycloak administration response was not valid bounded JSON.");
        }
    }
}
