using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Orbyss.Foundation.Json;

/// <summary>Implements bounded RFC 8785 canonical UTF-8 with explicit negative-zero rejection.</summary>
public static class JsonCanonicalizer
{
    /// <summary>Identifies RFC 8785 with verified erratum 7920 negative-zero rejection.</summary>
    public const string Algorithm = "rfc8785-reject-negative-zero-v1";

    /// <summary>Canonicalizes one value without product normalization or array reordering.</summary>
    public static byte[] Canonicalize(ReadOnlySpan<byte> input, string algorithm = Algorithm, int maxBytes = 1_048_576, int maxDepth = 32)
    {
        if (algorithm != Algorithm) throw new ArgumentException("Unknown canonicalization algorithm.", nameof(algorithm));
        var profile = new JsonProfile(new JsonProfileSettings { MaxBytes = maxBytes, MaxDepth = maxDepth });
        profile.ValidateInput(input);
        try
        {
            using var document = JsonDocument.Parse(input.ToArray(), new JsonDocumentOptions { MaxDepth = maxDepth, AllowDuplicateProperties = false });
            var output = new StringBuilder();
            AppendValue(output, document.RootElement);
            var bytes = new UTF8Encoding(false, true).GetBytes(output.ToString());
            if (bytes.Length > maxBytes) throw new JsonProfileException("json_size_exceeded");
            return bytes;
        }
        catch (JsonException) { throw new JsonProfileException("json_invalid_syntax"); }
        catch (EncoderFallbackException) { throw new JsonProfileException("json_invalid_unicode"); }
    }

    /// <summary>Writes ordered objects, unchanged arrays, exact strings, and binary64 numbers.</summary>
    private static void AppendValue(StringBuilder output, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                output.Append('{');
                var firstProperty = true;
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty) output.Append(',');
                    firstProperty = false;
                    AppendString(output, property.Name);
                    output.Append(':');
                    AppendValue(output, property.Value);
                }
                output.Append('}');
                break;
            case JsonValueKind.Array:
                output.Append('[');
                var firstItem = true;
                foreach (var item in value.EnumerateArray())
                {
                    if (!firstItem) output.Append(',');
                    firstItem = false;
                    AppendValue(output, item);
                }
                output.Append(']');
                break;
            case JsonValueKind.String: AppendString(output, value.GetString()!); break;
            case JsonValueKind.Number: output.Append(FormatNumber(value.GetRawText())); break;
            case JsonValueKind.True: output.Append("true"); break;
            case JsonValueKind.False: output.Append("false"); break;
            case JsonValueKind.Null: output.Append("null"); break;
            default: throw new JsonProfileException("json_invalid_syntax");
        }
    }

    /// <summary>Writes RFC 8785 string escapes without HTML or Unicode normalization.</summary>
    private static void AppendString(StringBuilder output, string text)
    {
        output.Append('"');
        foreach (var character in text)
        {
            switch (character)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\t': output.Append("\\t"); break;
                case '\n': output.Append("\\n"); break;
                case '\f': output.Append("\\f"); break;
                case '\r': output.Append("\\r"); break;
                default:
                    if (character < 32) output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    else output.Append(character);
                    break;
            }
        }
        output.Append('"');
    }

    /// <summary>Applies ECMAScript decimal/exponent placement to .NET's shortest round-trip binary64 digits.</summary>
    private static string FormatNumber(string token)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            throw new JsonProfileException("json_number_not_representable");
        if (number == 0)
        {
            if (token[0] == '-') throw new JsonProfileException("json_negative_zero");
            return "0";
        }
        var sign = number < 0 ? "-" : "";
        var roundTrip = Math.Abs(number).ToString("R", CultureInfo.InvariantCulture);
        var parts = roundTrip.Split('E', 'e');
        var exponent = parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        var point = parts[0].IndexOf('.');
        var position = (point < 0 ? parts[0].Length : point) + exponent;
        var digits = parts[0].Replace(".", "", StringComparison.Ordinal);
        var leading = digits.Length - digits.TrimStart('0').Length;
        digits = digits[leading..].TrimEnd('0');
        position -= leading;
        if (position > 0 && position <= 21)
            return sign + (position >= digits.Length ? digits.PadRight(position, '0') : digits.Insert(position, "."));
        if (position <= 0 && position > -6) return sign + "0." + new string('0', -position) + digits;
        var power = position - 1;
        return sign + digits[0] + (digits.Length > 1 ? "." + digits[1..] : "") +
            "e" + (power >= 0 ? "+" : "") + power.ToString(CultureInfo.InvariantCulture);
    }
}
