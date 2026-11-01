using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GapMouse.App.Localization;
using GapMouse.App.Services;
using GapMouse.Application.Paths;
using GapMouse.Application.Updates;

namespace GapMouse.App.ViewModels;

public sealed partial class AboutViewModel : ViewModelBase, IDisposable
{
    private enum Status
    {
        None,
        Checking,
        UpToDate,
        NoReleases,
        Failed,
        Available,
    }

    private readonly IUpdateChecker _updates;
    private readonly IShellLauncher _launcher;
    private readonly IUiDispatcher _ui;
    private readonly AppPaths _paths;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private Status _status = Status.None;
    private Version? _availableVersion;

    public AboutViewModel(Localizer l, IUpdateChecker updates, IShellLauncher launcher, IUiDispatcher ui, AppPaths paths, string version)
        : base(l)
    {
        _updates = updates;
        _launcher = launcher;
        _ui = ui;
        _paths = paths;
        VersionText = version;
        L.PropertyChanged += OnLocalizerChanged;
        _updates.UpdateFound += OnUpdateFound;
    }

    public string VersionText { get; }

    private const string Repository = "https://github.com/Gustavo-Piccoli/GapMouse";

#pragma warning disable CA1822 // Instance member so compiled bindings can reach it.
    public string RepositoryUrl => Repository;
#pragma warning restore CA1822

    public string LicenseText => L.AboutLicense;

    public string SettingsFilePath => _paths.SettingsFile;

    public string LogsDirectory => _paths.LogsDirectory;

    public string UpdateStatus => _status switch
    {
        Status.Checking => L.AboutChecking,
        Status.UpToDate => L.AboutUpToDate,
        Status.NoReleases => L.AboutNoReleases,
        Status.Failed => L.AboutCheckFailed,
        Status.Available => L.AboutUpdateAvailable(_availableVersion?.ToString() ?? string.Empty),
        _ => string.Empty,
    };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenReleaseCommand))]
    public partial Uri? AvailableReleaseUrl { get; private set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _updates.UpdateFound -= OnUpdateFound;
        L.PropertyChanged -= OnLocalizerChanged;
        _cts.Cancel();
        _cts.Dispose();
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        SetStatus(Status.Checking);
        AvailableReleaseUrl = null;
        UpdateCheckResult result;
        try
        {
            result = await _updates.CheckNowAsync(_cts.Token);
        }
        catch (Exception)
        {
            SetStatus(Status.Failed);
            return;
        }

        Apply(result);
    }

    [RelayCommand(CanExecute = nameof(CanOpenRelease))]
    private void OpenRelease()
    {
        if (AvailableReleaseUrl is { } url)
        {
            TryLaunch(() => _launcher.OpenUrl(url));
        }
    }

    [RelayCommand]
    private void OpenRepository() => TryLaunch(() => _launcher.OpenUrl(new Uri(Repository)));

    [RelayCommand]
    private void OpenSettingsFolder() => TryLaunch(() => _launcher.OpenFolder(_paths.ConfigDirectory));

    [RelayCommand]
    private void OpenLogsFolder() => TryLaunch(() => _launcher.OpenFolder(_paths.LogsDirectory));

    private static void TryLaunch(Action launch)
    {
        try
        {
            launch();
        }
        catch (Exception)
        {
            // No handler or shell failure: nothing useful to show, never crash the UI.
        }
    }

    private bool CanOpenRelease() => AvailableReleaseUrl is not null;

    private void Apply(UpdateCheckResult result)
    {
        switch (result.Outcome)
        {
            case UpdateCheckOutcome.UpdateAvailable when result.LatestVersion is not null:
                _availableVersion = result.LatestVersion;
                AvailableReleaseUrl = result.ReleaseUrl;
                SetStatus(Status.Available);
                break;
            case UpdateCheckOutcome.UpToDate:
                SetStatus(Status.UpToDate);
                break;
            case UpdateCheckOutcome.NoReleases:
                SetStatus(Status.NoReleases);
                break;
            default:
                SetStatus(Status.Failed);
                break;
        }
    }

    private void SetStatus(Status status)
    {
        _status = status;
        OnPropertyChanged(nameof(UpdateStatus));
    }

    private void OnUpdateFound(object? sender, UpdateCheckResult result) => _ui.Post(() => Apply(result));

    private void OnLocalizerChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(UpdateStatus));
        OnPropertyChanged(nameof(LicenseText));
    }
}
