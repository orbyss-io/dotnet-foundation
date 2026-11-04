namespace Orbyss.Foundation.Identity.Admin;

/// <summary>Reads effective user roles independently from optional administration write operations.</summary>
public interface IIdentityEffectiveUserRoleReader
{
    /// <summary>Gets the user's effective realm roles, including composite role expansion.</summary>
    ValueTask<IReadOnlyList<IdentityRole>> GetEffectiveUserRolesAsync(string userId, CancellationToken cancellationToken = default);
}
