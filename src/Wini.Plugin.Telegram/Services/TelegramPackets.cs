using WindowsIsland.Contracts;

namespace Wini.Plugin.Telegram.Services;

internal sealed class TelegramPackets(int processId)
{
    private byte[] _pending = [];
    private ulong _sequence;
    private readonly string _session = Guid.NewGuid().ToString("N");
    public bool Ready { get; private set; }
    public void Reset() { _pending = []; _sequence = 0; Ready = false; }

    public IReadOnlyList<PluginNotification> Feed(byte[] bytes, byte[]? icon)
    {
        if (bytes.Length > 1024 * 1024 || _pending.Length + bytes.Length > 2 * 1024 * 1024) throw new InvalidDataException();
        var all = new byte[_pending.Length + bytes.Length];
        _pending.CopyTo(all, 0); bytes.CopyTo(all, _pending.Length);
        var offset = 0;
        var result = new List<PluginNotification>();
        while (all.Length - offset >= TelegramHookProtocol.HeaderSize)
        {
            var packet = TelegramHookProtocol.ReadHeader(all.AsSpan(offset, TelegramHookProtocol.HeaderSize), processId);
            if (all.Length - offset - TelegramHookProtocol.HeaderSize < packet.PayloadLength) break;
            offset += TelegramHookProtocol.HeaderSize;
            var payload = all.AsSpan(offset, packet.PayloadLength).ToArray();
            offset += packet.PayloadLength;
            if (packet.Kind == 0) { Ready = true; continue; }
            if (packet.Kind != 1 || !Ready || packet.Sequence <= _sequence) continue;
            _sequence = packet.Sequence;
            var (title, body) = TelegramHookProtocol.ReadText(packet, payload);
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body)) continue;
            result.Add(new("wini_plugin_telegram", title, null, body, new DateTimeOffset(DateTime.FromFileTimeUtc(packet.FileTime)), icon,
                TelegramAvatar.Encode(packet, payload), $"dll/{_session}/{processId}/{packet.ObjectId}/{packet.Sequence}/{packet.FileTime}",
                processId, "Telegram", "telegram", (uint)packet.Sequence, Kind: PluginNotificationKind.ClientHook));
        }
        _pending = all.AsSpan(offset).ToArray();
        return result;
    }
}
