using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Wini.Plugin.Telegram.Services;

internal static class TelegramAvatar
{
    public static byte[]? Encode(TelegramHookPacket packet, byte[] payload)
    {
        if (packet.AvatarLength == 0) return null;
        if (packet.AvatarLength != 64 * 64 * 4 || payload.Length != packet.PayloadLength) throw new InvalidDataException();
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, 64);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 64);
        header[8] = 8; header[9] = 6;
        Chunk(png, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[1 + 64 * 4];
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var source = packet.TextLength + (y * 64 + x) * 4;
                    var target = 1 + x * 4;
                    var alpha = payload[source + 3];
                    row[target] = Straight(payload[source + 2], alpha);
                    row[target + 1] = Straight(payload[source + 1], alpha);
                    row[target + 2] = Straight(payload[source], alpha);
                    row[target + 3] = alpha;
                }
                zlib.Write(row);
            }
        }
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static byte Straight(byte value, byte alpha) => alpha == 0 ? (byte)0 : (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);
    private static void Chunk(Stream stream, string name, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length); stream.Write(number);
        var type = Encoding.ASCII.GetBytes(name); stream.Write(type); stream.Write(data);
        var crc = uint.MaxValue;
        foreach (var value in type.Concat(data))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        }
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); stream.Write(number);
    }
}
