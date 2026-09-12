using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Bounds and validates asset identity, bytes and media type before any route is published.</summary>
internal static partial class HostedAssetAdmission
{
    /// <summary>Validates unambiguous literal relative paths across Windows and Unix.</summary>
    public static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 512 || !SafePath().IsMatch(path) ||
            path.Split('/').Any(part => part is "." or ".." || part.EndsWith('.') ||
                part.Split('.')[0].ToUpperInvariant() is "CON" or "PRN" or "AUX" or "NUL" ||
                Regex.IsMatch(part, @"^(COM|LPT)[0-9](\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
            throw new InvalidDataException("Hosted asset path is not a literal descendant.");
    }
    /// <summary>Reads a bounded stream and checks the deployment's admitted hash.</summary>
    public static byte[] Read(IHostedAssetSource source, string path, string hash, int maximum)
    {
        ValidatePath(path);
        if (!Regex.IsMatch(hash ?? "", "^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Hosted asset hash is invalid.");
        using var input = source.OpenRead(path);
        using var output = new MemoryStream();
        var buffer = new byte[Math.Min(maximum + 1, 8192)];
        while (true)
        {
            var count = input.Read(buffer, 0, Math.Min(buffer.Length, maximum + 1 - (int)output.Length));
            if (count == 0) break;
            output.Write(buffer, 0, count);
            if (output.Length > maximum) throw new InvalidDataException("Hosted asset exceeds its byte budget.");
        }
        var bytes = output.ToArray();
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Hosted asset hash mismatch.");
        return bytes;
    }
    /// <summary>Checks content type against extension and distinct runtime/branding admission rules.</summary>
    public static void ValidateContent(HostedAssetDescriptor asset, byte[] data)
    {
        var extension = Path.GetExtension(asset.File);
        var expected = extension switch
        {
            ".js" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".png" => "image/png",
            ".woff2" => "font/woff2",
            _ => throw new InvalidDataException("Unsupported hosted media extension.")
        };
        if (asset.Visibility != "public" || asset.ContentType != expected ||
            (asset.Kind == "branding" ? extension != ".png" : asset.Kind != "runtime"))
            throw new InvalidDataException("Invalid public asset type or trust classification.");
        if (extension == ".png") PngAdmission.Validate(data);
        else if (extension == ".woff2")
        {
            if (data.Length < 48 || !data.AsSpan(0, 4).SequenceEqual("wOF2"u8) ||
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8, 4)) != data.Length)
                throw new InvalidDataException("Invalid WOFF2 header.");
        }
        else
        {
            try
            {
                var text = new UTF8Encoding(false, true).GetString(data);
                if (text.Contains('\0')) throw new InvalidDataException("Invalid runtime text.");
            }
            catch (DecoderFallbackException) { throw new InvalidDataException("Invalid runtime UTF-8."); }
        }
    }
    /// <summary>Matches only ordinary ASCII path segments, excluding encoding and alternate streams.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-][A-Za-z0-9_.-]*(/[A-Za-z0-9_-][A-Za-z0-9_.-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePath();
}
