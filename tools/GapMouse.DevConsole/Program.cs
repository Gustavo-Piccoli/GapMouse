using System.Collections.Concurrent;
using System.Text;
using GapMouse.Application.Engine;
using GapMouse.DevConsole;
using GapMouse.Domain.Pausing;
using GapMouse.Domain.Settings;
using GapMouse.Infrastructure.Randomness;
using GapMouse.Infrastructure.Win32;

Console.OutputEncoding = Encoding.UTF8;

ValidationResult<EngineSettings> parsed = ConsoleOptions.Parse(args, out IReadOnlyList<string> usageErrors);
if (usageErrors.Count > 0 || !parsed.IsValid)
{
    foreach (string error in usageErrors)
    {
        ConsoleOutput.WriteLine(ConsoleColor.Red, error);
    }

    foreach (SettingsError error in parsed.Errors)
    {
        ConsoleOutput.WriteLine(ConsoleColor.Red, $"Invalid {error.Field}: {error.Code}");
    }

    ConsoleOutput.WriteLine(ConsoleColor.Gray, "Usage: GapMouse.DevConsole [--idle <s>] [--check <s>] [--min <s>] [--max <s>] [--amplitude <px>] [--display on|off]");
    return 2;
}

using ManualResetEventSlim quit = new(false);
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    quit.Set();
};

using EngineHost host = new(
    parsed.Value,
    new Win32IdleTimeProvider(),
    new Win32CursorController(),
    new Win32MonitorBoundsProvider(),
    new Win32PowerRequestService(),
    new SystemRandomSource(),
    TimeProvider.System,
    new ConsoleLogger<EngineHost>());

host.EventRaised += (_, e) => ConsoleEventPrinter.Print(e.Event);
host.Start();

// Console.KeyAvailable throws when stdin is redirected, so fall back to a background reader in that case.
bool redirected = Console.IsInputRedirected;
ConcurrentQueue<char> redirectedKeys = new();
if (redirected)
{
    _ = Task.Run(() =>
    {
        int c;
        while ((c = Console.In.Read()) >= 0)
        {
            redirectedKeys.Enqueue((char)c);
        }
    });
}

while (!quit.Wait(100))
{
    char key;
    if (redirected)
    {
        if (!redirectedKeys.TryDequeue(out key))
        {
            continue;
        }
    }
    else if (Console.KeyAvailable)
    {
        key = Console.ReadKey(intercept: true).KeyChar;
    }
    else
    {
        continue;
    }

    switch (char.ToLowerInvariant(key))
    {
        case 'p':
            host.Pause(PauseDuration.Indefinite);
            break;
        case 't':
            host.Pause(PauseDuration.Custom(1));
            break;
        case 'r':
            host.Resume();
            break;
        case 'q':
            quit.Set();
            break;
    }
}

host.Dispose();
return 0;
