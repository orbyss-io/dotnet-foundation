using System.Security.Cryptography;
using System.Text;

namespace Orbyss.Foundation.Build;

/// <summary>Canonical settings source decoding and hashing, independent of host line endings.</summary>
public static class SettingsSourceText
{
    /// <summary>Reads strict UTF-8 with an optional initial UTF-8 BOM; replaces CRLF pairs only.</summary>
    /// <remarks>Lone CR, Unicode and final newline presence remain significant. Other encodings reject.</remarks>
    public static string Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
        return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>Hashes the canonical source text as UTF-8, without a BOM.</summary>
    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
