using System.Diagnostics;
using System.Security.Cryptography;
using WindowsIsland.PluginIsolation;
using Wini.Plugin.Telegram.Services;
namespace Wini.Plugin.Telegram.Tests;
internal static class TelegramHookInjector
{
    public static string Attach(int processId, string dll, string pipe, bool fixture = false)
    {
        using var verified = TelegramHookIntegrity.Open(dll);
        using var process = Process.GetProcessById(processId);
        var executable = process.MainModule!.FileName;
        if (fixture && Path.GetFileName(executable) != "WindowsIsland.TelegramFixture.exe") throw new InvalidOperationException();
        using var file = File.OpenRead(executable);
        var hash = Convert.ToHexString(SHA256.HashData(file));
        if (!fixture && hash is not ("D7680288560339FFE89FFEA10B603C87E35AAB1AD5DB5DD516E62D55FD5DA8DC" or "CEE6F5F0E1F9556E827FB65C9EB422DBEE3ECA7B7723A29D6F1502756E6CA666")) throw new NotSupportedException();
        return NativeBridgeInjector.Attach(processId, dll, pipe, TelegramHookIntegrity.Digest, hash, TelegramHookProtocol.Magic,
            TelegramHookProtocol.Version, fixture, process.StartTime.ToUniversalTime().Ticks);
    }
}
