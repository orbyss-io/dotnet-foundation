namespace Orbyss.Foundation.Authentication;

/// <summary>Owns the canonical dynamic permission-policy naming contract.</summary>
public static class PermissionPolicyNames
{
    /// <summary>Prefixes canonical application-permission policies.</summary>
    public const string Prefix = "permission:";

    /// <summary>Creates a policy name from one bounded canonical permission token.</summary>
    public static string ForPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission) || permission.Length > 256 ||
            permission.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            throw new ArgumentException("A canonical permission token is required.", nameof(permission));
        return Prefix + permission;
    }
}
