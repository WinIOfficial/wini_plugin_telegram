using System.Buffers.Binary;
using System.Text;
using Wini.Plugin.Telegram.Services;

internal static class PacketChecks
{
    public static void Run(Action<bool, string> check)
    {
        var parser = new TelegramPackets(42);
        var ready = Packet(0, 0);
        var handshakeMessages = 0;
        foreach (var value in ready) handshakeMessages += parser.Feed([value], null).Count;
        check(parser.Ready && handshakeMessages == 0, "A fragmented handshake establishes readiness without producing a notification");
        var message = Packet(1, 1);
        var result = new List<WindowsIsland.Contracts.PluginNotification>();
        foreach (var value in message) result.AddRange(parser.Feed([value], null));
        check(result is [{ ConversationName: "中文🚀", Body: "分包正文", OriginProcessId: 42, AppId: "telegram" }],
            "Byte-sized reads preserve Unicode text, process attribution and merge identity");
        check(parser.Feed(message, null).Count == 0, "Duplicate sequence values stay suppressed across separate reads");
        parser.Reset();
        check(!parser.Ready && parser.Feed(message, null).Count == 0, "A disconnected parser rejects messages before a fresh handshake");
        var wrong = Packet(0, 0); BinaryPrimitives.WriteUInt32LittleEndian(wrong.AsSpan(12), 43);
        var rejected = false;
        try { parser.Feed(wrong, null); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "A foreign process cannot supply bridge frames");

        var packet = new TelegramHookPacket(1, 42, 0, 1, DateTimeOffset.UtcNow.ToFileTime(), 0, 0, 64 * 64 * 4);
        var pixels = new byte[packet.AvatarLength];
        for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 25; pixels[i + 1] = 50; pixels[i + 2] = 100; pixels[i + 3] = 128; }
        Array.Clear(pixels, 0, 4);
        using var bitmap = new System.Drawing.Bitmap(new MemoryStream(TelegramAvatar.Encode(packet, pixels)!));
        var color = bitmap.GetPixel(1, 1);
        check(bitmap.GetPixel(0, 0).A == 0 && color.A == 128 && Math.Abs(color.R - 199) <= 1
            && Math.Abs(color.G - 100) <= 1 && Math.Abs(color.B - 50) <= 1,
            "Managed PNG encoding preserves transparent pixels and unpremultiplies native BGRA correctly");
    }

    private static byte[] Packet(uint kind, ulong sequence)
    {
        var title = kind == 1 ? "中文🚀" : ""; var body = kind == 1 ? "分包正文" : "";
        var bytes = new byte[56 + (title.Length + body.Length) * 2];
        void U(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        U(0, TelegramHookProtocol.Magic); U(4, 2); U(8, kind); U(12, 42); U(40, (uint)title.Length); U(44, (uint)body.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), sequence);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(32), DateTimeOffset.UtcNow.ToFileTime());
        Encoding.Unicode.GetBytes(title + body).CopyTo(bytes, 56);
        return bytes;
    }
}
