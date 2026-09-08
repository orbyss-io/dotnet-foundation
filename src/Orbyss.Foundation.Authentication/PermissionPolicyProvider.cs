using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.Authentication;

/// <summary>Builds authorization policies from stable <c>permission:name</c> identifiers.</summary>
internal sealed class PermissionPolicyProvider(
    IOptions<AuthorizationOptions> authorizationOptions,
    IOptions<FoundationWebOptions> webOptions)
    : DefaultAuthorizationPolicyProvider(authorizationOptions)
{
    /// <summary>Prefixes canonical application-permission policies.</summary>
    private const string Prefix = "permission:";

    /// <inheritdoc />
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        var permission = policyName[Prefix.Length..].Trim();
        if (permission.Length == 0)
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(webOptions.Value.PermissionClaim, permission)
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
