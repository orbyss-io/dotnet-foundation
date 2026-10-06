namespace Orbyss.Foundation.Json;

/// <summary>Identifies a deliberately selected configured profile rather than implicit global options.</summary>
public readonly record struct JsonProfileKey
{
    /// <summary>Gets the configuration key.</summary>
    public string Name { get; }
    /// <summary>Admits a bounded key without changing its identity.</summary>
    public JsonProfileKey(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length is < 1 or > 128 || !name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            throw new ArgumentException("JSON profile key must be a bounded token.", nameof(name));
        Name = name;
    }
}
