using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using WindowsIsland.Contracts;
using Wini.Plugin.Telegram.Services;
namespace Wini.Plugin.Telegram.Tests;
internal sealed class TelegramHookSession : IDisposable
{
    private readonly int _processId;
    private readonly Action<PluginNotification> _receive;
    private readonly NamedPipeServerStream _pipe;
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _reader;
    private bool _disposed;
    public bool IsConnected { get; private set; }
    public string PipeName { get; } = $"WindowsIsland.Bridge.{Environment.ProcessId}.{Guid.NewGuid():N}";
    public TelegramHookSession(int processId, Action<PluginNotification> receive)
    {
        _processId = processId; _receive = receive;
        _pipe = new(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _reader = ReadAsync();
    }
    public async Task AttachAsync(string path, bool fixture = false)
    {
        await Task.Run(() => TelegramHookInjector.Attach(_processId, path, PipeName, fixture));
        await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), _stop.Token);
    }
    private async Task ReadAsync()
    {
        try
        {
            await _pipe.WaitForConnectionAsync(_stop.Token);
            if (!GetNamedPipeClientProcessId(_pipe.SafePipeHandle, out var id) || id != _processId) throw new InvalidDataException();
            var packets = new TelegramPackets(_processId);
            var buffer = new byte[16384];
            while (!_stop.IsCancellationRequested)
            {
                var count = await _pipe.ReadAsync(buffer, _stop.Token);
                if (count == 0) break;
                foreach (var notification in packets.Feed(buffer.AsSpan(0, count).ToArray(), null)) _receive(notification);
                if (packets.Ready) { IsConnected = true; _ready.TrySetResult(); }
            }
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException or ArgumentException) { _ready.TrySetException(error); }
        finally { IsConnected = false; }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _stop.Cancel(); _pipe.Dispose();
        _ = _reader.ContinueWith(_ => _stop.Dispose(), TaskScheduler.Default);
    }
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint id);
}
