using System.Diagnostics;
using Wini.Plugin.Telegram.Tests;
using Wini.Plugin.Telegram.Services;
using WindowsIsland.Contracts;

internal static class LiveCapture
{
    public static async Task RunAsync(string dll, bool attachOnly = false)
    {
        using var client = Process.GetProcessesByName("Telegram").Single();
        var message = new TaskCompletionSource<PluginNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var session = new TelegramHookSession(client.Id, notification => message.TrySetResult(notification)))
        {
            await session.AttachAsync(dll);
            Console.WriteLine("PASS: Verified Telegram executable and connected the native DLL bridge");
            if (attachOnly)
            {
                await Task.Delay(2000);
                if (client.HasExited || !session.IsConnected)
                    throw new InvalidOperationException("Telegram exited or disconnected after attaching the bridge.");
                Console.WriteLine("PASS: The real Telegram executable remains alive with its verified capture profile attached");
            }
            else
            {
            var notification = await message.Task.WaitAsync(TimeSpan.FromSeconds(60));
            if (string.IsNullOrWhiteSpace(notification.Body) || string.IsNullOrWhiteSpace(notification.ConversationName))
                throw new InvalidOperationException("The actual Telegram notification did not supply title and body.");
            Console.WriteLine($"PASS: Actual Telegram popup supplied original UTF-16 title ({notification.ConversationName.Length} chars) and body ({notification.Body.Length} chars)");
            if (notification.SenderAvatar is not { Length: > 0 }) throw new InvalidOperationException("Telegram avatar buffer was unavailable.");
            Console.WriteLine($"PASS: The actual popup supplied its avatar image ({notification.SenderAvatar.Length} PNG bytes)");
            }
        }
        var timeout = Stopwatch.StartNew();
        bool Loaded()
        {
            client.Refresh();
            return client.Modules.Cast<ProcessModule>().Any(module => module.ModuleName == "WindowsIsland.TelegramHook.dll");
        }
        while (Loaded() && timeout.Elapsed.TotalSeconds < 4) await Task.Delay(50);
        if (Loaded() || client.HasExited) throw new InvalidOperationException("Live capture did not release the DLL cleanly.");
        Console.WriteLine("PASS: Capture stopped and the DLL unloaded with Telegram still running");
    }
}
