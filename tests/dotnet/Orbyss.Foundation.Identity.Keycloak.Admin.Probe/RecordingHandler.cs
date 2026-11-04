using System.Net;
using System.Text;
/// <summary>Emulates the small Keycloak Admin REST surface exercised by the public probe.</summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    /// <summary>Gets immutable snapshots of outbound requests.</summary>
    internal List<RecordedRequest> Requests { get; } = [];

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.PathAndQuery;
        Requests.Add(new(request.Method.Method, path, body, request.Headers.Authorization?.ToString()));
        if (path.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
        {
            Require(request.Headers.Authorization?.Scheme == "Basic", "service-account authentication must use client_secret_basic");
            return Json(HttpStatusCode.OK, "{\"access_token\":\"admin-token\",\"expires_in\":120}");
        }
        Require(request.Headers.Authorization?.ToString() == "Bearer admin-token", "Admin REST request omitted its bearer token");
        if (request.Method == HttpMethod.Get && path.EndsWith("/clients/client-owned", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, "{\"id\":\"client-owned\",\"clientId\":\"portal\",\"enabled\":true,\"serviceAccountsEnabled\":true,\"secret\":\"provider-secret-must-not-be-replayed\",\"attributes\":{\"owner\":\"release\",\"operator\":\"retained\"},\"authenticationFlowBindingOverrides\":{\"browser\":\"operator-flow\"}}");
        if (request.Method == HttpMethod.Get && path.Contains("/clients?", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, "[{\"id\":\"client-owned\",\"clientId\":\"portal\",\"attributes\":{\"owner\":\"release\"}}]");
        if (request.Method == HttpMethod.Get && path.EndsWith("/client-scopes/scope-owned", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, "{\"id\":\"scope-owned\",\"name\":\"owned-scope\",\"attributes\":{\"operator\":\"retained\"}}");
        if (request.Method == HttpMethod.Get && path.EndsWith("/protocol-mappers/models/mapper-owned", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, "{\"id\":\"mapper-owned\",\"name\":\"owned-mapper\",\"config\":{\"operator\":\"retained\"}}");
        if (request.Method == HttpMethod.Get && path.EndsWith("/roles/owned-role", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, "{\"id\":\"role-owned\",\"name\":\"owned-role\",\"attributes\":{\"operator\":[\"retained\"]}}");
        if (request.Method == HttpMethod.Get && path.EndsWith("/roles?briefRepresentation=false", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, "[{\"id\":\"role-owned\",\"name\":\"owned-role\",\"attributes\":{\"owner\":[\"release\"]}}]");
        if (request.Method == HttpMethod.Get && path.EndsWith("/admin/realms/tenant%20realm", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, "{\"realm\":\"tenant realm\",\"enabled\":true,\"attributes\":{\"operator-owned\":\"preserved\"}}");
        if (request.Method == HttpMethod.Post && path.EndsWith("/admin/realms", StringComparison.Ordinal))
            return new HttpResponseMessage(HttpStatusCode.Created);
        if (request.Method == HttpMethod.Get && path.EndsWith("/role-mappings/realm/composite", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, "[{\"id\":\"role-1\",\"name\":\"subscriber\",\"composite\":true}]");
        if (request.Method == HttpMethod.Get && path.Contains("/users?", StringComparison.Ordinal)) return Json(HttpStatusCode.OK, "[{\"id\":\"user-1\",\"username\":\"alice\",\"enabled\":true,\"emailVerified\":false}]");
        if (request.Method == HttpMethod.Get && path.EndsWith("/users/count", StringComparison.Ordinal)) return Json(HttpStatusCode.OK, "1");
        if (request.Method == HttpMethod.Get && path.EndsWith("/roles", StringComparison.Ordinal)) return Json(HttpStatusCode.OK, "[{\"id\":\"role-1\",\"name\":\"subscriber\"}]");
        if (request.Method == HttpMethod.Get && path.Contains("/groups", StringComparison.Ordinal)) return Json(HttpStatusCode.OK, "[{\"id\":\"group-1\",\"name\":\"paid\",\"path\":\"/paid\"}]");
        if (request.Method == HttpMethod.Get && path.EndsWith("/client-scopes", StringComparison.Ordinal)) return Json(HttpStatusCode.OK, "[{\"id\":\"scope-1\",\"name\":\"billing.read\"}]");
        if (request.Method == HttpMethod.Get && path.EndsWith("/sessions", StringComparison.Ordinal)) return Json(HttpStatusCode.OK, "[]");
        if (request.Method == HttpMethod.Get && path.EndsWith("/keys", StringComparison.Ordinal)) return Json(HttpStatusCode.OK, "{\"active\":{\"RSA\":\"kid-1\"}}");
        if (request.Method == HttpMethod.Get) return Json(HttpStatusCode.OK, "[]");
        if (request.Method == HttpMethod.Post && path.EndsWith("/client-secret", StringComparison.Ordinal)) return Json(HttpStatusCode.OK, "{\"value\":\"rotated-secret\"}");
        if (request.Method == HttpMethod.Post && IsCreatePath(path))
        {
            var id = path.EndsWith("/users", StringComparison.Ordinal) ? "user-created" : "resource-created";
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri($"https://identity.example/admin/realms/tenant/resources/{id}");
            return response;
        }
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    /// <summary>Identifies create endpoints whose contracts return a Location header.</summary>
    private static bool IsCreatePath(string path) =>
        path.EndsWith("/users", StringComparison.Ordinal)
        || path.EndsWith("/clients", StringComparison.Ordinal)
        || path.EndsWith("/client-scopes", StringComparison.Ordinal)
        || path.EndsWith("/protocol-mappers/models", StringComparison.Ordinal)
        || path.EndsWith("/authentication/flows", StringComparison.Ordinal)
        || path.EndsWith("/copy", StringComparison.Ordinal)
        || path.EndsWith("/executions/execution", StringComparison.Ordinal)
        || path.EndsWith("/identity-provider/instances", StringComparison.Ordinal)
        || path.EndsWith("/organizations", StringComparison.Ordinal);

    /// <summary>Creates a JSON response with the requested status.</summary>
    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Fails the probe when an outbound security invariant is violated.</summary>
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
