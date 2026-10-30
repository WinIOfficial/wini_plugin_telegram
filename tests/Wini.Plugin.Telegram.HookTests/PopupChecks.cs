using WindowsIsland.Contracts;
using Wini.Plugin.Telegram.Services;

internal static class PopupChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var desktop = new Desktop();
        var popups = new TelegramPopups();
        DesktopClient Client(params long[] handles) => new(42, 1, "fixture", "1", [], handles.Select(id =>
            new DesktopWindow(id, "Notification", new(0, 0, 300, 100), 0, 8, false, true, false, false, 1)).ToArray());
        check((await popups.ReadAsync(desktop, Client(100), default)).Count == 0, "Existing notification popups form a silent connection baseline");
        var messages = await popups.ReadAsync(desktop, Client(100, 101), default);
        check(messages is [{ ConversationName: "合成会话", Body: "弹窗正文", OriginProcessId: 42 }], "A new popup supplies its original title, body and tracked click process");
        check((await popups.ReadAsync(desktop, Client(100, 101), default)).Count == 0, "Unchanged popup snapshots do not replay messages");
        await popups.ReadAsync(desktop, Client(100), default);
        var reopened = await popups.ReadAsync(desktop, Client(100, 101), default);
        check(reopened.Count == 1 && reopened[0].DeduplicationKey != messages[0].DeduplicationKey,
            "A closed and reopened popup establishes a distinct event even with identical text");
        var restarted = new TelegramPopups();
        check((await restarted.ReadAsync(desktop, Client(100, 101), default)).Count == 0,
            "Reconnecting popup fallback never replays already visible notifications");
    }

    private sealed class Desktop : IPluginDesktop
    {
        public Task<DesktopTree> ReadTreeAsync(int processId, long window, CancellationToken token = default)
        {
            DesktopNode Node(int parent, string text, int type) => new(parent, text, "", "", type, "", "", "", default, false);
            return Task.FromResult(new DesktopTree("fixture", [Node(-1, "Telegram", 50032), Node(0, "合成会话", 50020), Node(0, "弹窗正文", 50020)]));
        }
        public Task<DesktopClient[]> FindClientsAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<byte[]> ReadModuleAsync(DesktopRead request, CancellationToken token = default) => throw new NotSupportedException();
        public Task<byte[]> ReadMemoryAsync(DesktopRead request, CancellationToken token = default) => throw new NotSupportedException();
        public Task<bool> SetFlagAsync(DesktopFlag request, CancellationToken token = default) => throw new NotSupportedException();
        public Task<byte[]?> ReadImageAsync(DesktopImage request, CancellationToken token = default) => throw new NotSupportedException();
    }
}
