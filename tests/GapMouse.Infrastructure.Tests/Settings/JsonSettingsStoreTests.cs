using GapMouse.Application.Settings;
using GapMouse.Domain.Settings;
using GapMouse.Infrastructure.Settings;
using GapMouse.Testing.IO;

namespace GapMouse.Infrastructure.Tests.Settings;

public class JsonSettingsStoreTests
{
    private const string FileName = "GapMouse.settings.json";

    [Fact]
    public void Load_MissingFile_ReturnsDefaultsAndFileExistedFalse()
    {
        using var dir = new TempDirectory();

        var result = new JsonSettingsStore(Path.Combine(dir.Path, FileName)).Load();

        result.Settings.ShouldBe(AppSettings.Default);
        result.FileExisted.ShouldBeFalse();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        using var dir = new TempDirectory();
        var store = new JsonSettingsStore(Path.Combine(dir.Path, FileName));
        var settings = new AppSettings
        {
            Engine = EngineSettings.Create(300, 5, 20, 40, 200, false).Value,
            StartWithWindows = true,
            StartMinimized = true,
            NotificationsEnabled = true,
            PauseHotkey = "Ctrl+Shift+K",
            CheckForUpdates = false,
            Theme = AppTheme.Light,
            Language = UiLanguage.Portuguese,
            SaveLogsToFile = false,
            LogRetentionDays = 30,
            TrayHintShown = true,
        };

        store.Save(settings);
        var result = store.Load();

        result.Settings.ShouldBe(settings);
        result.FileExisted.ShouldBeTrue();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaultsKeepsBadCopyAndWarns()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(path, "{ not json");

        var result = new JsonSettingsStore(path).Load();

        result.Settings.ShouldBe(AppSettings.Default);
        result.FileExisted.ShouldBeTrue();
        result.Warnings.Count.ShouldBe(1);
        File.ReadAllText(path + ".bad").ShouldBe("{ not json");
        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public void Load_InvalidEngineSection_UsesEngineDefaultsKeepsOtherFields()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(path, """
            { "schemaVersion": 1,
              "engine": { "movementIntervalMinSeconds": 30, "movementIntervalMaxSeconds": 10 },
              "appearance": { "theme": "Light" } }
            """);

        var result = new JsonSettingsStore(path).Load();

        result.Settings.Engine.ShouldBe(EngineSettings.Default);
        result.Settings.Theme.ShouldBe(AppTheme.Light);
        result.Warnings.Count.ShouldBe(1);
        File.Exists(path + ".bad").ShouldBeFalse();
    }

    [Fact]
    public void Load_Missing_ReadFailedFalse()
    {
        using var dir = new TempDirectory();

        new JsonSettingsStore(Path.Combine(dir.Path, FileName)).Load().ReadFailed.ShouldBeFalse();
    }

    [Fact]
    public void Load_UnknownTheme_ResetsOnlyTheme()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(path, """
            { "appearance": { "theme": "Purple", "language": "English" }, "system": { "startWithWindows": true } }
            """);

        var result = new JsonSettingsStore(path).Load();

        result.Settings.Theme.ShouldBe(AppTheme.Dark);
        result.Settings.Language.ShouldBe(UiLanguage.English);
        result.Settings.StartWithWindows.ShouldBeTrue();
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldContain("Purple");
    }

    [Fact]
    public void Load_RetentionOutOfRange_UsesSeven()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(path, """{ "logs": { "retentionDays": 0, "saveToFile": false } }""");

        var result = new JsonSettingsStore(path).Load();

        result.Settings.LogRetentionDays.ShouldBe(7);
        result.Settings.SaveLogsToFile.ShouldBeFalse();
        result.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void Load_WithoutKeepAwakeWhileLocked_UsesDefaultWithoutWarning()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(
            path,
            """{"schemaVersion":1,"engine":{"idleThresholdSeconds":300,"checkIntervalSeconds":5,"movementIntervalMinSeconds":20,"movementIntervalMaxSeconds":40,"amplitudePixels":200,"keepDisplayOn":false}}""");

        var result = new JsonSettingsStore(path).Load();

        result.Settings.Engine.KeepAwakeWhileLocked.ShouldBeFalse();
        result.Settings.Engine.AmplitudePixels.ShouldBe(200);
        result.Warnings.ShouldBeEmpty();
        result.FileExisted.ShouldBeTrue();
    }

    [Fact]
    public void SaveThenLoad_RoundTripsKeepAwakeWhileLocked()
    {
        using var dir = new TempDirectory();
        var store = new JsonSettingsStore(Path.Combine(dir.Path, FileName));
        var settings = AppSettings.Default with { Engine = EngineSettings.Create(120, 1, 10, 25, 120, true, true).Value };

        store.Save(settings);
        var result = store.Load();

        result.Settings.Engine.KeepAwakeWhileLocked.ShouldBeTrue();
        result.Settings.ShouldBe(settings);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Load_MissingSections_UsesDefaultsWithoutWarnings()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(path, """{"schemaVersion":1}""");

        var result = new JsonSettingsStore(path).Load();

        result.Settings.ShouldBe(AppSettings.Default);
        result.FileExisted.ShouldBeTrue();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Load_CommentsAndTrailingCommas_Accepted()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(path, """
            {
              // hand edited
              "appearance": { "theme": "Light", },
            }
            """);

        var result = new JsonSettingsStore(path).Load();

        result.Settings.Theme.ShouldBe(AppTheme.Light);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Load_NeverReadsTmpFile()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(path, """{ "appearance": { "theme": "Light" } }""");
        File.WriteAllText(path + ".tmp", "garbage");

        var result = new JsonSettingsStore(path).Load();

        result.Settings.Theme.ShouldBe(AppTheme.Light);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Load_FileLocked_SetsReadFailed()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(path, """{ "appearance": { "theme": "Light" } }""");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = new JsonSettingsStore(path).Load();

        result.Settings.ShouldBe(AppSettings.Default);
        result.FileExisted.ShouldBeTrue();
        result.Warnings.Count.ShouldBe(1);
        File.Exists(path + ".bad").ShouldBeFalse();
        result.ReadFailed.ShouldBeTrue();
    }

    [Fact]
    public void Load_CorruptFileCannotBeMoved_ReturnsDefaultsWithWarning()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, FileName);
        File.WriteAllText(path, "{ not json");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var result = new JsonSettingsStore(path).Load();

        result.Settings.ShouldBe(AppSettings.Default);
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldNotContain("copy was kept");
        File.Exists(path + ".bad").ShouldBeFalse();
    }
}
