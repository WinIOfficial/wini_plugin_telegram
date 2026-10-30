using Microsoft.Win32.SafeHandles;
using WindowsIsland.PluginIsolation;
using Wini.Plugin.Telegram;
namespace Wini.Plugin.Telegram.Tests;
internal sealed class TelegramHookIntegrity(NativeBridgeIntegrity inner) : IDisposable
{
    public SafeFileHandle Handle => inner.Handle;
    internal static string Digest { get { using var stream = typeof(TelegramPlugin).Assembly.GetManifestResourceStream("WindowsIsland.TelegramHook.sha256")!; using var reader = new StreamReader(stream); return reader.ReadToEnd().Trim(); } }
    public static TelegramHookIntegrity Open(string path) => new(NativeBridgeIntegrity.Open(path, Digest));
    public void Dispose() => inner.Dispose();
}
