using System.Net;
using System.Text;
using System.Text.Json;

/// <summary>Models Keycloak's supplied-entry merge and explicit null deletion independently of the SDK implementation.</summary>
internal sealed class ConfigurationRemovalHandler : HttpMessageHandler
{
    /// <summary>Gets the client's independent attributes, including an operator-owned MFA setting.</summary>
    internal Dictionary<string, string> Attributes { get; private set; } = new(StringComparer.Ordinal)
        { ["owned"] = "retire", ["operator"] = "keep", ["mfa"] = "required" };
    /// <summary>Gets independent authentication flow bindings.</summary>
    internal Dictionary<string, string> Bindings { get; private set; } = new(StringComparer.Ordinal)
        { ["browser"] = "retire", ["direct_grant"] = "operator-mfa" };
    /// <summary>Gets outbound snapshots for mutation and rejection assertions.</summary>
    internal List<RecordedRequest> Requests { get; } = [];

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        Requests.Add(new(request.Method.Method, path, body, request.Headers.Authorization?.ToString()));
        if (path.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
            return Json(new { access_token = "fictional-token", expires_in = 120 });
        if (request.Headers.Authorization?.ToString() != "Bearer fictional-token")
            throw new InvalidOperationException("Removal omitted the native authenticated transport.");
        if (request.Method == HttpMethod.Get)
            return Json(new { id = "client", clientId = "portal", enabled = true,
                secret = "provider-secret-must-not-be-replayed", attributes = Attributes, authenticationFlowBindingOverrides = Bindings });
        if (request.Method == HttpMethod.Put)
        {
            using var payload = JsonDocument.Parse(body!);
            if (payload.RootElement.TryGetProperty("attributes", out var attributes))
                MergeEntries(Attributes, attributes);
            if (payload.RootElement.TryGetProperty("authenticationFlowBindingOverrides", out var bindings))
                MergeEntries(Bindings, bindings);
        }
        return new(HttpStatusCode.NoContent);
    }

    /// <summary>Processes only supplied keys; omitted entries survive, and null or empty values remove named entries.</summary>
    private static void MergeEntries(Dictionary<string, string> current, JsonElement changes)
    {
        foreach (var entry in changes.EnumerateObject())
        {
            var value = entry.Value.GetString();
            if (string.IsNullOrEmpty(value)) current.Remove(entry.Name);
            else current[entry.Name] = value;
        }
    }

    /// <summary>Produces an independently serialized provider response.</summary>
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
}
