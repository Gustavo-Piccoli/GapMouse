using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using GapMouse.App.Localization;
using GapMouse.App.Services;
using GapMouse.Application.Logging;

namespace GapMouse.App.ViewModels;

public sealed class LogsViewModel : ViewModelBase, IDisposable
{
    private readonly InMemoryActivityLog _log;
    private readonly IUiDispatcher _ui;
    private readonly IClipboardService _clipboard;
    private readonly IShellLauncher _launcher;
    private readonly string _logsDirectory;
    private readonly List<LogEntry> _entries;
    private LogFilter _filter = LogFilter.All;
    private bool _disposed;

    public LogsViewModel(
        Localizer l,
        InMemoryActivityLog log,
        IUiDispatcher ui,
        IClipboardService clipboard,
        IShellLauncher launcher,
        string logsDirectory)
        : this(l, log, ui, clipboard, launcher, logsDirectory, null)
    {
    }

    // afterSubscribe is a test hook that runs between subscribing and taking the snapshot.
    internal LogsViewModel(
        Localizer l,
        InMemoryActivityLog log,
        IUiDispatcher ui,
        IClipboardService clipboard,
        IShellLauncher launcher,
        string logsDirectory,
        Action? afterSubscribe)
        : base(l)
    {
        _log = log;
        _ui = ui;
        _clipboard = clipboard;
        _launcher = launcher;
        _logsDirectory = logsDirectory;

        // Subscribe first so an entry appended meanwhile is never lost; the handler skips entries the snapshot already holds.
        _log.EntryAdded += OnEntryAdded;
        afterSubscribe?.Invoke();
        _entries = [.. log.GetEntries()];

        CopyCommand = new AsyncRelayCommand(CopyAsync);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        ClearCommand = new RelayCommand(Clear);

        Rebuild();
        L.PropertyChanged += OnLocalizerChanged;
    }

    public ObservableCollection<LogLineViewModel> Lines { get; } = [];

    public LogFilter Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                OnPropertyChanged(nameof(FilterIndex));
                Rebuild();
            }
        }
    }

    /// <summary>Index form of <see cref="Filter"/> for the view's selector.</summary>
    public int FilterIndex
    {
        get => (int)_filter;
        set => Filter = Enum.IsDefined((LogFilter)value) ? (LogFilter)value : LogFilter.All;
    }

    public IAsyncRelayCommand CopyCommand { get; }

    public IRelayCommand OpenFolderCommand { get; }

    public IRelayCommand ClearCommand { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _log.EntryAdded -= OnEntryAdded;
        L.PropertyChanged -= OnLocalizerChanged;
    }

    private static bool Matches(LogFilter filter, LogKind kind) => filter switch
    {
        LogFilter.All => true,
        LogFilter.Movement => kind is LogKind.Waiting or LogKind.Moving or LogKind.Return,
        LogFilter.Pauses => kind is LogKind.Paused or LogKind.Resumed,
        LogFilter.System => kind is LogKind.Start or LogKind.Stop or LogKind.Config or LogKind.Warn or LogKind.Error,
        _ => true,
    };

    private void OnEntryAdded(object? sender, LogEntry entry) => _ui.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        if (_entries.Any(x => ReferenceEquals(x, entry)))
        {
            return;
        }

        _entries.Add(entry);
        if (_entries.Count > InMemoryActivityLog.Capacity)
        {
            _entries.RemoveAt(0);
        }

        if (Matches(_filter, entry.Kind))
        {
            Lines.Add(ToLine(entry));
            while (Lines.Count > InMemoryActivityLog.Capacity)
            {
                Lines.RemoveAt(0);
            }
        }
    });

    private void OnLocalizerChanged(object? sender, PropertyChangedEventArgs e) => Rebuild();

    private LogLineViewModel ToLine(LogEntry entry) => new(
        entry.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        entry.Tag,
        LogMessageLocalizer.Format(entry, L),
        entry.Kind);

    private void Rebuild()
    {
        Lines.Clear();
        foreach (var entry in _entries.Where(e => Matches(_filter, e.Kind)).TakeLast(InMemoryActivityLog.Capacity))
        {
            Lines.Add(ToLine(entry));
        }
    }

    private async Task CopyAsync()
    {
        var text = string.Join(Environment.NewLine, Lines.Select(x => $"{x.Time} | {x.Tag} | {x.Message}"));
        try
        {
            await _clipboard.SetTextAsync(text);
        }
#pragma warning disable CA1031 // The clipboard can fail in many ways (COM, no top level); the UI must not crash.
        catch (Exception)
#pragma warning restore CA1031
        {
            // Nothing useful to show here.
        }
    }

    private void OpenFolder()
    {
        try
        {
            _launcher.OpenFolder(_logsDirectory);
        }
#pragma warning disable CA1031 // The launcher can throw many types; the UI must not crash.
        catch (Exception)
#pragma warning restore CA1031
        {
            // No handler for the folder; nothing useful to show here.
        }
    }

    private void Clear()
    {
        _entries.Clear();
        Lines.Clear();
    }
}
