using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using GapMouse.Application.Engine;
using GapMouse.Application.Logging;
using GapMouse.Application.Paths;
using GapMouse.Application.Settings;
using GapMouse.Application.State;
using GapMouse.Application.Updates;
using GapMouse.DevConsole;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;
using GapMouse.Domain.Settings;
using GapMouse.Infrastructure.Logging;
using GapMouse.Infrastructure.Randomness;
using GapMouse.Infrastructure.Settings;
using GapMouse.Infrastructure.State;
using GapMouse.Infrastructure.Updates;
using GapMouse.Infrastructure.Win32;

Console.OutputEncoding = Encoding.UTF8;

// The dev console keeps its data next to its own exe, so it never touches the real user profile.
AppPaths paths = AppPaths.Portable(AppContext.BaseDirectory);

SettingsLoadResult loaded = new JsonSettingsStore(paths.SettingsFile).Load();
foreach (string warning in loaded.Warnings)
{
    ConsoleOutput.WriteLine(ConsoleColor.Yellow, $"WARN | {warning}");
}

// Flags override the saved settings for this run only; they are never saved.
ValidationResult<EngineSettings> parsed = ConsoleOptions.Parse(args, loaded.Settings.Engine, out IReadOnlyList<string> usageErrors);
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

AppSettings settings = loaded.Settings;
FileActivityLog fileLog = new(paths.LogsDirectory, TimeProvider.System);
fileLog.Configure(settings.SaveLogsToFile, settings.LogRetentionDays);

DateOnly today = DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);
JsonAppStateStore stateStore = new(paths.StateFile);
DailyStats? restoredStats = stateStore.Load()?.TodayOrNull(today);

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
    new ActivityLogger<EngineHost>(fileLog, TimeProvider.System, new ConsoleLogger<EngineHost>()),
    restoredStats);

ActivityLogWriter writer = new(host, fileLog);
AppStatePersister persister = new(host, stateStore, TimeProvider.System, new ConsoleLogger<AppStatePersister>());
persister.Start();

host.EventRaised += (_, e) =>
{
    ConsoleEventPrinter.Print(LogEntryFormatter.FromEvent(e.Event));
    if (e.Event is EngineStarted)
    {
        ConsoleOutput.WriteLine(ConsoleColor.Gray, "p = pause, t = pause 1 min, r = resume, q = quit");
    }
};
host.Start();

// The update check never blocks startup: it runs in the background and prints one line when it completes.
using CancellationTokenSource updateCts = new();
HttpClient? http = null;
UpdateService? updates = null;
Task updateTask = Task.CompletedTask;
if (settings.CheckForUpdates)
{
    string productVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    Version current = ReleaseVersion.TryParse(productVersion, out Version? parsedVersion) ? parsedVersion : new Version(0, 0, 0);
    http = new HttpClient();
    updates = new UpdateService(new GitHubReleaseSource(http, productVersion), current, TimeProvider.System, new ConsoleLogger<UpdateService>());
    updateTask = RunUpdateCheckAsync(updates, updateCts.Token);
}

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

// Cancel an in-flight update check and give it a moment to finish before disposing what it uses.
updateCts.Cancel();
try
{
    updateTask.Wait(TimeSpan.FromSeconds(2));
}
catch (AggregateException)
{
    // RunUpdateCheckAsync observes its own failures; nothing else can surface here.
}

host.Dispose();
persister.Dispose();
writer.Dispose();
updates?.Dispose();
http?.Dispose();
return 0;

static async Task RunUpdateCheckAsync(UpdateService updates, CancellationToken cancellationToken)
{
    try
    {
        UpdateCheckResult result = await updates.CheckNowAsync(cancellationToken).ConfigureAwait(false);
        (ConsoleColor Color, string Text) line = result.Outcome switch
        {
            UpdateCheckOutcome.UpdateAvailable => (ConsoleColor.Green, $"[UPDATE] | Version {result.LatestVersion} is available: {result.ReleaseUrl}"),
            UpdateCheckOutcome.UpToDate => (ConsoleColor.DarkGray, "[UPDATE] | You are up to date"),
            UpdateCheckOutcome.NoReleases => (ConsoleColor.DarkGray, "[UPDATE] | No releases published yet"),
            _ => (ConsoleColor.DarkGray, "[UPDATE] | Could not check for updates"),
        };
        ConsoleOutput.WriteLine(line.Color, line.Text);
    }
    catch (OperationCanceledException)
    {
        // The console is exiting.
    }
#pragma warning disable CA1031 // Background task: any failure must be observed, never crash the process.
    catch (Exception ex)
#pragma warning restore CA1031
    {
        ConsoleOutput.WriteLine(ConsoleColor.DarkGray, $"[UPDATE] | Could not check for updates: {ex.Message}");
    }
}
