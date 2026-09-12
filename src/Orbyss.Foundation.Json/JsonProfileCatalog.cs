using System.Collections.Frozen;

namespace Orbyss.Foundation.Json;

/// <summary>Holds an immutable named catalog scoped to its owning application or shell.</summary>
public sealed class JsonProfileCatalog
{
    /// <summary>Stores the compiled profiles.</summary>
    private readonly FrozenDictionary<string, JsonProfile> profiles;
    /// <summary>Compiles all profiles and allowlisted registrations.</summary>
    public JsonProfileCatalog(IReadOnlyDictionary<string, JsonProfileSettings> settings, IEnumerable<IJsonProfileExtension>? extensions = null)
    {
        var registered = (extensions ?? []).ToArray();
        profiles = settings.ToFrozenDictionary(item => item.Key, item => new JsonProfile(item.Value, registered), StringComparer.Ordinal);
        if (profiles.Count is 0 or > 128) throw new InvalidOperationException("Foundation:Json requires 1 to 128 profiles.");
    }
    /// <summary>Gets a deliberately selected named profile or fails closed.</summary>
    public JsonProfile Get(string name) => profiles.TryGetValue(name, out var profile)
        ? profile : throw new InvalidOperationException("Foundation:Json selects an unknown profile.");
}
