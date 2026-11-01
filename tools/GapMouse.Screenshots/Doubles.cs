using GapMouse.App.Services;
using GapMouse.Application.Settings;
using GapMouse.Application.Updates;

namespace GapMouse.Screenshots;

internal sealed class InMemorySettingsService : ISettingsService
{
    public AppSettings Current { get; private set; } = AppSettings.Default;

    public bool IsHotkeyRegistered => true;

    public event EventHandler<AppSettings>? Changed;

    public void Initialize()
    {
    }

    public SettingsSaveResult Save(AppSettings settings)
    {
        Current = settings;
        Changed?.Invoke(this, settings);
        return new SettingsSaveResult(Saved: true, StartupApplied: true, HotkeyRegistered: true);
    }

    public void MarkTrayHintShown()
    {
    }
}

internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

internal sealed class NoopPausePrompt : IPausePrompt
{
    public Task<int?> AskMinutesAsync() => Task.FromResult<int?>(null);
}

internal sealed class NoopAppLifetime : IAppLifetime
{
    public void Exit()
    {
    }
}

internal sealed class NoopShellLauncher : IShellLauncher
{
    public void OpenFolder(string path)
    {
    }

    public void OpenUrl(Uri url)
    {
    }
}

internal sealed class NoopClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Task.CompletedTask;
}

internal sealed class NeverUpdateChecker : IUpdateChecker
{
    public event EventHandler<UpdateCheckResult>? UpdateFound
    {
        add { }
        remove { }
    }

    public Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new UpdateCheckResult(UpdateCheckOutcome.UpToDate, null, null));
}
