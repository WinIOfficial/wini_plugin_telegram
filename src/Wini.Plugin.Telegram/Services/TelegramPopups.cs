using WindowsIsland.Contracts;

namespace Wini.Plugin.Telegram.Services;

internal sealed class TelegramPopups
{
    private readonly Dictionary<long, (string Title, string Body)?> _windows = [];
    private bool _baseline;
    private uint _sequence;
    private readonly string _session = Guid.NewGuid().ToString("N");
    public bool Readable { get; private set; }

    public async Task<IReadOnlyList<PluginNotification>> ReadAsync(IPluginDesktop desktop, DesktopClient client, CancellationToken token)
    {
        var windows = client.Windows.Where(IsPopup).ToArray();
        var result = new List<PluginNotification>();
        foreach (var id in _windows.Keys.Where(id => !windows.Any(w => w.Handle == id)).ToArray()) _windows.Remove(id);
        foreach (var window in windows)
        {
            if (!_baseline) { _windows[window.Handle] = null; continue; }
            // Popups already present when listening starts remain a silent baseline until they close.
            if (_windows.TryGetValue(window.Handle, out var previous) && previous is null) continue;
            try
            {
                var tree = await desktop.ReadTreeAsync(client.ProcessId, window.Handle, token).ConfigureAwait(false);
                Readable = tree.Nodes.Length > 0;
                var labels = tree.Nodes.Skip(1).Where(n => !n.Offscreen && n.ControlType is 50020 or 50030)
                    .Select(n => n.Name.Trim()).Where(t => t.Length > 0 && t is not ("Telegram" or "Telegram Desktop"))
                    .Select(t => t.Length > 2048 ? t[..2048] : t).Distinct(StringComparer.Ordinal).Take(32).ToArray();
                if (labels.Length == 0) continue;
                var content = labels.Length == 1 ? (tree.Nodes[0].Name, labels[0]) : (labels[0], string.Join("\n", labels.Skip(1)));
                if (previous == content) continue;
                _windows[window.Handle] = content;
                result.Add(new("wini_plugin_telegram", content.Item1, null, content.Item2, DateTimeOffset.UtcNow, client.Icon,
                    DeduplicationKey: $"popup/{_session}/{client.ProcessId}/{window.Handle}/{++_sequence}", OriginProcessId: client.ProcessId,
                    AppName: "Telegram", AppId: "telegram", Id: _sequence, Kind: PluginNotificationKind.ClientHook));
            }
            catch (IOException) { }
        }
        _baseline = true;
        return result;
    }

    private static bool IsPopup(DesktopWindow window)
    {
        if (!window.Visible || window.Foreground || window.Minimized || window.Bounds.IsEmpty) return false;
        var scale = Math.Max(1, window.Scale);
        var width = window.Bounds.Width / scale; var height = window.Bounds.Height / scale;
        if (width is < 120 or > 640 || height is < 40 or > 380 || (window.Style & 0x70000) != 0) return false;
        var name = window.ClassName;
        if (name is "#32768" or "#32770" || name.Contains("tooltip", StringComparison.OrdinalIgnoreCase)
            || name.Contains("QTipLabel", StringComparison.OrdinalIgnoreCase)) return false;
        return name.Contains("notification", StringComparison.OrdinalIgnoreCase) || name.Contains("notify", StringComparison.OrdinalIgnoreCase)
            || name.Contains("toast", StringComparison.OrdinalIgnoreCase)
            || (window.ExtendedStyle & 8) != 0 && ((window.ExtendedStyle & 0x80) != 0 || window.Owned);
    }
}
