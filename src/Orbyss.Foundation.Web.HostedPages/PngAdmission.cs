using System.Buffers.Binary;
using System.IO.Compression;

namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Admits bounded, non-interlaced 8-bit RGB/RGBA PNGs with checked chunks and scanlines.</summary>
internal static class PngAdmission
{
    /// <summary>Rejects malformed, oversized, appended, or unsupported PNG content before public serving.</summary>
    public static void Validate(byte[] bytes)
    {
        if (bytes.Length < 57 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
            throw new InvalidDataException("Invalid PNG signature.");
        var offset = 8;
        var width = 0;
        var height = 0;
        var channels = 0;
        var ended = false;
        using var compressed = new MemoryStream();
        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < 12) throw new InvalidDataException("Truncated PNG.");
            var lengthValue = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (lengthValue > bytes.Length - offset - 12) throw new InvalidDataException("Truncated PNG chunk.");
            var length = (int)lengthValue;
            var kind = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            if (Crc(bytes.AsSpan(offset + 4, length + 4)) != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + length, 4)))
                throw new InvalidDataException("PNG checksum mismatch.");
            if (offset == 8 && kind != "IHDR") throw new InvalidDataException("PNG header missing.");
            if (kind == "IHDR")
            {
                if (offset != 8 || length != 13) throw new InvalidDataException("Invalid PNG header.");
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8, 4)));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 12, 4)));
                channels = bytes[offset + 17] switch { 2 => 3, 6 => 4, _ => 0 };
                if (width is < 1 or > 8192 || height is < 1 or > 8192 || (long)width * height > 8_000_000 ||
                    bytes[offset + 16] != 8 || channels == 0 || bytes[offset + 18] != 0 || bytes[offset + 19] != 0 || bytes[offset + 20] != 0)
                    throw new InvalidDataException("Unsupported PNG dimensions or encoding.");
            }
            else if (kind == "IDAT") compressed.Write(bytes, offset + 8, length);
            else if (kind == "IEND")
            {
                if (length != 0 || offset + 12 != bytes.Length) throw new InvalidDataException("Invalid PNG end.");
                ended = true;
            }
            else if (kind is not ("sRGB" or "gAMA" or "pHYs"))
                throw new InvalidDataException("Unsupported PNG metadata; publish a stripped RGB/RGBA PNG.");
            offset += length + 12;
        }
        if (!ended || compressed.Length == 0) throw new InvalidDataException("Incomplete PNG.");
        compressed.Position = 0;
        using var inflater = new ZLibStream(compressed, CompressionMode.Decompress);
        var row = new byte[width * channels];
        for (var y = 0; y < height; y++)
        {
            var filter = inflater.ReadByte();
            if (filter is < 0 or > 4) throw new InvalidDataException("Invalid PNG filter.");
            inflater.ReadExactly(row);
        }
        if (inflater.ReadByte() != -1) throw new InvalidDataException("PNG decoded size mismatch.");
    }
    /// <summary>Computes the PNG CRC-32 over chunk type and data.</summary>
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        return ~crc;
    }
}
