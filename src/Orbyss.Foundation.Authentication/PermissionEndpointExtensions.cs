using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace Orbyss.Foundation.Authentication;

/// <summary>Applies feature-owned permission tokens through the existing canonical policy provider.</summary>
public static class PermissionEndpointExtensions
{
    /// <summary>Requires the canonical application permission without interpreting provider roles or ownership.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        var policy = PermissionPolicyNames.ForPermission(permission);
        builder.RequireAuthorization(policy);
        return builder;
    }
}
