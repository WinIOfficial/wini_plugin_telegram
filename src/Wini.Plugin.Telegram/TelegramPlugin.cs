using WindowsIsland.Contracts;
using Wini.Plugin.Telegram.Services;

namespace Wini.Plugin.Telegram;

public sealed class TelegramPlugin : IIslandPlugin, IPluginPolling, IPluginStatusProvider, IPluginRetry
{
    private IPluginContext? _context;
    private IPluginDesktop? _desktop;
    private TelegramService? _service;
    private CancellationTokenSource? _stop;
    private Task? _run;
    private bool _disposed;
    private int _generation;
    public string Id => "wini_plugin_telegram";
    public string DisplayName => "Telegram";
    public PluginStatus Status => _service?.Status ?? new(PluginConnectionState.NotRunning, PluginStatusReason.ClientNotDetected);
    public bool CanRetry => !_disposed && _service is not null && Status.State == PluginConnectionState.Disconnected;

    public Task InitializeAsync(IPluginContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (context.PluginId != Id) throw new ArgumentException("Plugin context identity mismatch.");
        _desktop = context.GetService<IPluginDesktop>() ?? throw new NotSupportedException("Desktop service is unavailable.");
        _context = context;
        return Task.CompletedTask;
    }

    public Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_run is not null) return Task.CompletedTask;
        var context = _context ?? throw new InvalidOperationException();
        var generation = Volatile.Read(ref _generation);
        _stop = new();
        var token = _stop.Token;
        _service = new(_desktop ?? throw new InvalidOperationException(), notification =>
        {
            if (!_disposed && !token.IsCancellationRequested && generation == Volatile.Read(ref _generation))
                context.PublishNotification(notification);
        }, error => context.Diagnostics.Write(new(Id, "Listener", AdditionalInformation:
            new PluginDiagnosticInfo(ExceptionType: error.GetType().FullName, HResult: error.HResult))));
        _run = Task.Run(() => _service.RunAsync(token));
        return Task.CompletedTask;
    }

    public Task PollAsync() => _run is { IsFaulted: true } failed ? failed : Task.CompletedTask;
    public void Retry() => _service?.Retry();
    public async Task StopAsync()
    {
        Interlocked.Increment(ref _generation);
        var stop = _stop; var run = _run;
        _stop = null; _run = null; _service = null;
        if (stop is null) return;
        stop.Cancel();
        try { if (run is not null) await run.ConfigureAwait(false); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { stop.Dispose(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        _context = null; _desktop = null;
    }
}
