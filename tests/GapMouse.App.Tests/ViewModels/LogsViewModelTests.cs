using GapMouse.App.Localization;
using GapMouse.App.Services;
using GapMouse.App.Tests.Fakes;
using GapMouse.App.ViewModels;
using GapMouse.Application.Logging;
using GapMouse.Application.Settings;
using GapMouse.Domain.Engine;

namespace GapMouse.App.Tests.ViewModels;

public sealed class LogsViewModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 9, 30, 15, TimeSpan.Zero);

    private readonly InMemoryActivityLog _log = new();
    private readonly Localizer _l = new(() => new System.Globalization.CultureInfo("en-US"));
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeLauncher _launcher = new();

    private LogsViewModel Create() =>
        new(_l, _log, new ImmediateUiDispatcher(), _clipboard, _launcher, "C:/logs");

    private static LogEntry Entry(LogKind kind, string message = "m", EngineEvent? source = null) =>
        new(T0, kind, message, source);

    [Fact]
    public void Constructor_LoadsExistingEntries()
    {
        _log.Append(Entry(LogKind.Start));
        _log.Append(Entry(LogKind.Moving));

        using var vm = Create();

        vm.Lines.Count.ShouldBe(2);
        vm.Lines[0].Tag.ShouldBe("START");
        vm.Lines[0].Time.ShouldBe("09:30:15");
    }

    [Fact]
    public void EntryAdded_AppendsLine()
    {
        using var vm = Create();

        _log.Append(Entry(LogKind.Warn, "careful"));

        vm.Lines.Count.ShouldBe(1);
        vm.Lines[0].Message.ShouldBe("careful");
        vm.Lines[0].Kind.ShouldBe(LogKind.Warn);
    }

    [Fact]
    public void Filter_Movement_ShowsOnlyMovementKinds()
    {
        foreach (var kind in Enum.GetValues<LogKind>())
        {
            _log.Append(Entry(kind));
        }

        using var vm = Create();
        vm.Filter = LogFilter.Movement;

        vm.Lines.Select(x => x.Kind).ShouldBe([LogKind.Waiting, LogKind.Moving, LogKind.Return]);
    }

    [Fact]
    public void Filter_System_IncludesWarnAndError()
    {
        foreach (var kind in Enum.GetValues<LogKind>())
        {
            _log.Append(Entry(kind));
        }

        using var vm = Create();
        vm.Filter = LogFilter.System;

        vm.Lines.Select(x => x.Kind).ShouldBe(
            [LogKind.Start, LogKind.Config, LogKind.Stop, LogKind.Warn, LogKind.Error],
            ignoreOrder: true);

        vm.Filter = LogFilter.Pauses;
        vm.Lines.Select(x => x.Kind).ShouldBe([LogKind.Paused, LogKind.Resumed]);
    }

    [Fact]
    public void Clear_HidesExistingButShowsNew()
    {
        _log.Append(Entry(LogKind.Start));
        using var vm = Create();

        vm.ClearCommand.Execute(null);
        vm.Lines.ShouldBeEmpty();
        _log.GetEntries().Count.ShouldBe(1);

        _log.Append(Entry(LogKind.Stop));
        vm.Lines.Count.ShouldBe(1);
        vm.Lines[0].Tag.ShouldBe("STOP");
    }

    [Fact]
    public async Task Copy_CopiesVisibleLinesInPipeFormat()
    {
        _log.Append(Entry(LogKind.Start, "one"));
        _log.Append(Entry(LogKind.Moving, "two"));
        using var vm = Create();
        vm.Filter = LogFilter.Movement;

        await vm.CopyCommand.ExecuteAsync(null);

        _clipboard.Text.ShouldBe("09:30:15 | MOVING | two");

        vm.Filter = LogFilter.All;
        await vm.CopyCommand.ExecuteAsync(null);
        _clipboard.Text.ShouldBe($"09:30:15 | START | one{Environment.NewLine}09:30:15 | MOVING | two");
    }

    [Fact]
    public void OpenFolder_OpensLogsDirectory()
    {
        using var vm = Create();

        vm.OpenFolderCommand.Execute(null);

        _launcher.Folders.ShouldBe(["C:/logs"]);
    }

    [Fact]
    public void Portuguese_LocalizesEngineEventMessages()
    {
        _l.SetLanguage(UiLanguage.Portuguese);
        var moving = new MovingStarted(T0, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(25));
        _log.Append(LogEntryFormatter.FromEvent(moving));
        using var vm = Create();

        vm.Lines[0].Message.ShouldBe("Movendo o mouse a cada 10–25 s");
        vm.Lines[0].Tag.ShouldBe("MOVING");
    }

    [Fact]
    public void NonEngineEntry_ShowsRawMessage()
    {
        _l.SetLanguage(UiLanguage.Portuguese);
        _log.Append(Entry(LogKind.Error, "Disk exploded"));
        using var vm = Create();

        vm.Lines[0].Message.ShouldBe("Disk exploded");
    }

    [Fact]
    public void LanguageChange_RerendersLines()
    {
        _log.Append(LogEntryFormatter.FromEvent(new EngineStopped(T0)));
        using var vm = Create();
        vm.Lines[0].Message.ShouldBe("GapMouse stopped");

        _l.SetLanguage(UiLanguage.Portuguese);

        vm.Lines[0].Message.ShouldBe("GapMouse encerrado");
    }

    [Fact]
    public void MoreThanCapacity_KeepsLastThousand()
    {
        using var vm = Create();

        for (var i = 0; i < 1100; i++)
        {
            _log.Append(Entry(LogKind.Moving, $"m{i}"));
        }

        vm.Lines.Count.ShouldBe(1000);
        vm.Lines[0].Message.ShouldBe("m100");
        vm.Lines[^1].Message.ShouldBe("m1099");
    }

    [Fact]
    public void Dispose_StopsListening()
    {
        var vm = Create();
        vm.Dispose();

        _log.Append(Entry(LogKind.Start));

        vm.Lines.ShouldBeEmpty();
    }

    private sealed class FakeClipboard : IClipboardService
    {
        public string? Text { get; private set; }

        public Task SetTextAsync(string text)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLauncher : IShellLauncher
    {
        public List<string> Folders { get; } = [];

        public void OpenFolder(string path) => Folders.Add(path);

        public void OpenUrl(Uri url) => throw new NotSupportedException();
    }
}
