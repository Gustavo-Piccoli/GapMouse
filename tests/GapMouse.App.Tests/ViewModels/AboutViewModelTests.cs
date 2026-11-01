using System.Globalization;
using GapMouse.App.Localization;
using GapMouse.App.Services;
using GapMouse.App.Tests.Fakes;
using GapMouse.App.ViewModels;
using GapMouse.Application.Paths;
using GapMouse.Application.Settings;
using GapMouse.Application.Updates;

namespace GapMouse.App.Tests.ViewModels;

public sealed class AboutViewModelTests
{
    private static readonly Uri ReleaseUrl = new("https://github.com/Gustavo-Piccoli/GapMouse/releases/tag/v1.2.0");

    private readonly FakeUpdateChecker _updates = new();
    private readonly FakeLauncher _launcher = new();
    private readonly AppPaths _paths = new(@"C:\cfg", @"C:\cfg\logs", IsPortable: false);
    private readonly Localizer _localizer = new(() => CultureInfo.GetCultureInfo("en-US"));

    private AboutViewModel Create() =>
        new(_localizer, _updates, _launcher, new ImmediateUiDispatcher(), _paths, "0.1.0");

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var vm = Create();

        vm.Dispose();

        Should.NotThrow(vm.Dispose);
    }

    [Fact]
    public async Task CheckUpdates_UpdateAvailable_ShowsVersionAndUrl()
    {
        _updates.Result = new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, new Version(1, 2, 0), ReleaseUrl);
        var vm = Create();

        await vm.CheckUpdatesCommand.ExecuteAsync(null);

        vm.UpdateStatus.ShouldBe("Version 1.2.0 is available.");
        vm.AvailableReleaseUrl.ShouldBe(ReleaseUrl);
    }

    [Fact]
    public async Task CheckUpdates_UpToDate_ShowsMessage()
    {
        _updates.Result = new UpdateCheckResult(UpdateCheckOutcome.UpToDate, new Version(0, 1, 0), ReleaseUrl);
        var vm = Create();

        await vm.CheckUpdatesCommand.ExecuteAsync(null);

        vm.UpdateStatus.ShouldBe("You're up to date.");
        vm.AvailableReleaseUrl.ShouldBeNull();
    }

    [Fact]
    public async Task CheckUpdates_NoReleases_ShowsMessage()
    {
        _updates.Result = new UpdateCheckResult(UpdateCheckOutcome.NoReleases, null, null);
        var vm = Create();

        await vm.CheckUpdatesCommand.ExecuteAsync(null);

        vm.UpdateStatus.ShouldBe("No releases published yet.");
    }

    [Fact]
    public async Task CheckUpdates_Failed_ShowsMessage()
    {
        _updates.Result = new UpdateCheckResult(UpdateCheckOutcome.Failed, null, null);
        var vm = Create();

        await vm.CheckUpdatesCommand.ExecuteAsync(null);

        vm.UpdateStatus.ShouldBe("Could not check for updates.");
    }

    [Fact]
    public async Task CheckUpdates_CheckerThrows_ShowsFailedMessage()
    {
        _updates.Exception = new InvalidOperationException("boom");
        var vm = Create();

        await vm.CheckUpdatesCommand.ExecuteAsync(null);

        vm.UpdateStatus.ShouldBe("Could not check for updates.");
        vm.CheckUpdatesCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task CheckUpdates_WhileRunning_CommandDisabled()
    {
        var gate = new TaskCompletionSource<UpdateCheckResult>();
        _updates.Gate = gate;
        var vm = Create();

        var running = vm.CheckUpdatesCommand.ExecuteAsync(null);

        vm.CheckUpdatesCommand.CanExecute(null).ShouldBeFalse();
        vm.UpdateStatus.ShouldBe("Checking…");

        gate.SetResult(new UpdateCheckResult(UpdateCheckOutcome.UpToDate, new Version(0, 1, 0), null));
        await running;

        vm.CheckUpdatesCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void UpdateFoundEvent_ShowsAvailable()
    {
        var vm = Create();

        _updates.RaiseUpdateFound(new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, new Version(1, 2, 0), ReleaseUrl));

        vm.UpdateStatus.ShouldBe("Version 1.2.0 is available.");
        vm.AvailableReleaseUrl.ShouldBe(ReleaseUrl);
    }

    [Fact]
    public void OpenRelease_OpensUrl()
    {
        var vm = Create();
        _updates.RaiseUpdateFound(new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, new Version(1, 2, 0), ReleaseUrl));

        vm.OpenReleaseCommand.Execute(null);

        _launcher.OpenedUrls.ShouldBe([ReleaseUrl]);
    }

    [Fact]
    public void OpenRelease_LauncherThrows_DoesNotCrash()
    {
        var vm = Create();
        _updates.RaiseUpdateFound(new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, new Version(1, 2, 0), ReleaseUrl));
        _launcher.Throw = true;

        Should.NotThrow(() => vm.OpenReleaseCommand.Execute(null));
    }

    [Fact]
    public void OpenSettingsFolder_OpensConfigDirectory()
    {
        var vm = Create();

        vm.OpenSettingsFolderCommand.Execute(null);

        _launcher.OpenedFolders.ShouldBe([@"C:\cfg"]);
    }

    [Fact]
    public void OpenLogsFolder_OpensLogsDirectory()
    {
        var vm = Create();

        vm.OpenLogsFolderCommand.Execute(null);

        _launcher.OpenedFolders.ShouldBe([@"C:\cfg\logs"]);
    }

    [Fact]
    public void OpenRepository_OpensRepositoryUrl()
    {
        var vm = Create();

        vm.OpenRepositoryCommand.Execute(null);

        _launcher.OpenedUrls.ShouldBe([new Uri("https://github.com/Gustavo-Piccoli/GapMouse")]);
    }

    [Fact]
    public void Constants_ExposePathsAndVersion()
    {
        var vm = Create();

        vm.VersionText.ShouldBe("0.1.0");
        vm.RepositoryUrl.ShouldBe("https://github.com/Gustavo-Piccoli/GapMouse");
        vm.SettingsFilePath.ShouldBe(_paths.SettingsFile);
        vm.LogsDirectory.ShouldBe(@"C:\cfg\logs");
        vm.LicenseText.ShouldBe("MIT License");
    }

    [Fact]
    public async Task Portuguese_Texts()
    {
        _localizer.SetLanguage(UiLanguage.Portuguese);
        _updates.Result = new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, new Version(1, 2, 0), ReleaseUrl);
        var vm = Create();

        vm.LicenseText.ShouldBe("Licença MIT");
        await vm.CheckUpdatesCommand.ExecuteAsync(null);
        vm.UpdateStatus.ShouldBe("Versão 1.2.0 disponível.");

        _updates.Result = new UpdateCheckResult(UpdateCheckOutcome.UpToDate, new Version(0, 1, 0), null);
        await vm.CheckUpdatesCommand.ExecuteAsync(null);
        vm.UpdateStatus.ShouldBe("Você está na versão mais recente.");

        _updates.Result = new UpdateCheckResult(UpdateCheckOutcome.NoReleases, null, null);
        await vm.CheckUpdatesCommand.ExecuteAsync(null);
        vm.UpdateStatus.ShouldBe("Nenhuma versão publicada ainda.");

        _updates.Result = new UpdateCheckResult(UpdateCheckOutcome.Failed, null, null);
        await vm.CheckUpdatesCommand.ExecuteAsync(null);
        vm.UpdateStatus.ShouldBe("Não foi possível verificar atualizações.");
    }

    [Fact]
    public async Task LanguageChange_RefreshesStatus()
    {
        _updates.Result = new UpdateCheckResult(UpdateCheckOutcome.UpToDate, new Version(0, 1, 0), null);
        var vm = Create();
        await vm.CheckUpdatesCommand.ExecuteAsync(null);

        _localizer.SetLanguage(UiLanguage.Portuguese);

        vm.UpdateStatus.ShouldBe("Você está na versão mais recente.");
    }

    [Fact]
    public void Dispose_UnsubscribesFromUpdateFound()
    {
        var vm = Create();

        vm.Dispose();
        _updates.RaiseUpdateFound(new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, new Version(1, 2, 0), ReleaseUrl));

        vm.AvailableReleaseUrl.ShouldBeNull();
    }

    private sealed class FakeUpdateChecker : IUpdateChecker
    {
        public UpdateCheckResult Result { get; set; } = new(UpdateCheckOutcome.UpToDate, null, null);

        public Exception? Exception { get; set; }

        public TaskCompletionSource<UpdateCheckResult>? Gate { get; set; }

        public event EventHandler<UpdateCheckResult>? UpdateFound;

        public Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken)
        {
            if (Exception is not null)
            {
                return Task.FromException<UpdateCheckResult>(Exception);
            }

            return Gate?.Task ?? Task.FromResult(Result);
        }

        public void RaiseUpdateFound(UpdateCheckResult result) => UpdateFound?.Invoke(this, result);
    }

    private sealed class FakeLauncher : IShellLauncher
    {
        public List<string> OpenedFolders { get; } = [];

        public List<Uri> OpenedUrls { get; } = [];

        public bool Throw { get; set; }

        public void OpenFolder(string path)
        {
            if (Throw)
            {
                throw new InvalidOperationException();
            }

            OpenedFolders.Add(path);
        }

        public void OpenUrl(Uri url)
        {
            if (Throw)
            {
                throw new InvalidOperationException();
            }

            OpenedUrls.Add(url);
        }
    }
}
