using WindowsIsland.Contracts;

namespace Wini.Plugin.Telegram.Services;

internal sealed class TelegramService(IPluginDesktop desktop, Action<PluginNotification> publish, Action<Exception> diagnose)
{
    private sealed class Client(DesktopClient info)
    {
        public DesktopClient Info = info;
        public readonly TelegramPackets Packets = new(info.ProcessId);
        public readonly TelegramPopups Popups = new();
        public bool Open, Ready, Unsupported;
        public DateTimeOffset RetryAt;
    }
    private readonly Dictionary<int, Client> _clients = [];
    private int _retry;
    private PluginStatus _status = new(PluginConnectionState.NotRunning, PluginStatusReason.ClientNotDetected);
    public PluginStatus Status => Volatile.Read(ref _status);
    public void Retry() => Interlocked.Exchange(ref _retry, 1);
    private static readonly string[] SupportedHashes =
    [
        "D7680288560339FFE89FFEA10B603C87E35AAB1AD5DB5DD516E62D55FD5DA8DC",
        "CEE6F5F0E1F9556E827FB65C9EB422DBEE3ECA7B7723A29D6F1502756E6CA666"
    ];

    private static string HookDigest()
    {
        using var stream = typeof(TelegramService).Assembly.GetManifestResourceStream("WindowsIsland.TelegramHook.sha256")
            ?? throw new InvalidDataException();
        using var reader = new StreamReader(stream);
        var digest = reader.ReadToEnd().Trim();
        if (digest.Length != 64 || !digest.All(Uri.IsHexDigit)) throw new InvalidDataException();
        return digest;
    }

    public async Task RunAsync(CancellationToken token)
    {
        var digest = HookDigest();
        var refresh = DateTimeOffset.MinValue;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var now = DateTimeOffset.UtcNow;
                    var retry = Interlocked.Exchange(ref _retry, 0) != 0;
                    if (now >= refresh || retry)
                    {
                        var clients = await desktop.FindClientsAsync(token).ConfigureAwait(false);
                        foreach (var id in _clients.Keys.Where(id => !clients.Any(c => c.ProcessId == id)).ToArray()) _clients.Remove(id);
                        foreach (var info in clients)
                        {
                            if (!_clients.TryGetValue(info.ProcessId, out var client) || client.Info.StartedAt != info.StartedAt)
                                _clients[info.ProcessId] = client = new(info);
                            client.Info = info;
                            if (retry) { client.Unsupported = false; client.RetryAt = DateTimeOffset.MinValue; }
                            if (!client.Open && !client.Unsupported && now >= client.RetryAt)
                                await OpenAsync(client, digest, token).ConfigureAwait(false);
                        }
                        refresh = now.AddSeconds(1);
                    }
                    foreach (var client in _clients.Values)
                    {
                        if (client.Open)
                        {
                            var data = await desktop.ReadBridgeAsync(client.Info.ProcessId, token).ConfigureAwait(false);
                            foreach (var message in client.Packets.Feed(data.Bytes, client.Info.Icon)) publish(message);
                            client.Ready = client.Packets.Ready && data.Connected;
                            if (!data.Connected)
                            {
                                await desktop.CloseBridgeAsync(client.Info.ProcessId, token).ConfigureAwait(false);
                                client.Open = false; client.RetryAt = now.AddSeconds(5); client.Packets.Reset();
                            }
                        }
                        if (!client.Ready)
                            foreach (var message in await client.Popups.ReadAsync(desktop, client.Info, token).ConfigureAwait(false)) publish(message);
                    }
                    Volatile.Write(ref _status, _clients.Count == 0
                        ? new(PluginConnectionState.NotRunning, PluginStatusReason.ClientNotDetected)
                        : _clients.Values.Any(c => c.Ready || c.Popups.Readable)
                            ? new(PluginConnectionState.Connected)
                            : new(PluginConnectionState.Disconnected, _clients.Values.Any(c => c.Unsupported)
                                ? PluginStatusReason.VersionNotSupported : PluginStatusReason.ConnectionFailed));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    diagnose(error);
                    Volatile.Write(ref _status, new(PluginConnectionState.Disconnected, PluginStatusReason.ConnectionFailed));
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
                await Task.Delay(100, token).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var client in _clients.Values.Where(c => c.Open))
            {
                try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1)); await desktop.CloseBridgeAsync(client.Info.ProcessId, timeout.Token).ConfigureAwait(false); }
                catch (Exception) { /* Host revocation also closes all bridges. */ }
            }
            _clients.Clear();
        }
    }

    private async Task OpenAsync(Client client, string digest, CancellationToken token)
    {
        client.RetryAt = DateTimeOffset.UtcNow.AddSeconds(10);
        foreach (var hash in SupportedHashes)
        {
            try
            {
                client.Open = await desktop.OpenBridgeAsync(new(client.Info.ProcessId, "WindowsIsland.TelegramHook.dll", digest,
                    hash, TelegramHookProtocol.Magic, TelegramHookProtocol.Version), token).ConfigureAwait(false);
                if (client.Open) return;
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                // The host sends sanitized exceptions over RPC. Try the second complete binary profile.
                diagnose(error);
                return;
            }
        }
        client.Unsupported = true;
    }
}
