using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using GapMouse.App.Localization;
using GapMouse.Application.Settings;
using GapMouse.Application.Shell;
using GapMouse.Domain.Settings;

namespace GapMouse.App.ViewModels;

/// <summary>The Settings tab: an editable copy of the settings with inline validation and explicit save.</summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    // Mirrors the ranges enforced by EngineSettings.Create; a test guards against drift.
    internal static readonly IReadOnlyDictionary<SettingsField, (int Min, int Max)> EngineRanges =
        new Dictionary<SettingsField, (int Min, int Max)>
        {
            [SettingsField.IdleThreshold] = (10, 3600),
            [SettingsField.CheckInterval] = (1, 60),
            [SettingsField.MovementIntervalMin] = (1, 3600),
            [SettingsField.MovementIntervalMax] = (1, 3600),
            [SettingsField.Amplitude] = (10, 500),
        };

    private readonly ISettingsService _settings;
    private readonly RelayCommand _saveCommand;

    private string _idleText = string.Empty;
    private string _checkText = string.Empty;
    private string _minText = string.Empty;
    private string _maxText = string.Empty;
    private string _amplitudeText = string.Empty;
    private string _retentionText = string.Empty;
    private string _hotkeyText = string.Empty;
    private bool _keepDisplayOn;
    private bool _startWithWindows;
    private bool _startMinimized;
    private bool _notificationsEnabled;
    private bool _checkForUpdates;
    private bool _saveLogsToFile;
    private AppTheme _theme;
    private UiLanguage _language;

    private string? _idleError;
    private string? _checkError;
    private string? _minError;
    private string? _maxError;
    private string? _amplitudeError;
    private string? _retentionError;
    private string? _hotkeyError;

    private bool _isDirty;
    private bool _loading;
    private bool _saving;
    private StatusKind _status;

    public SettingsViewModel(Localizer l, ISettingsService settings)
        : base(l)
    {
        _settings = settings;
        _saveCommand = new RelayCommand(Save, () => IsDirty && !HasErrors);
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);

        LoadFrom(settings.Current);
        settings.Changed += OnSettingsChanged;
        l.PropertyChanged += OnLocalizerChanged;
    }

    private enum StatusKind
    {
        None,
        Saved,
        NotWritten,
        StartupFailed,
        HotkeyFailed,
    }

    public IRelayCommand SaveCommand => _saveCommand;

    public IRelayCommand RestoreDefaultsCommand { get; }

    public string IdleText
    {
        get => _idleText;
        set => Edit(ref _idleText, value);
    }

    public string CheckText
    {
        get => _checkText;
        set => Edit(ref _checkText, value);
    }

    public string MinText
    {
        get => _minText;
        set => Edit(ref _minText, value);
    }

    public string MaxText
    {
        get => _maxText;
        set => Edit(ref _maxText, value);
    }

    public string AmplitudeText
    {
        get => _amplitudeText;
        set => Edit(ref _amplitudeText, value);
    }

    public string RetentionText
    {
        get => _retentionText;
        set => Edit(ref _retentionText, value);
    }

    public string HotkeyText
    {
        get => _hotkeyText;
        set => Edit(ref _hotkeyText, value);
    }

    public bool KeepDisplayOn
    {
        get => _keepDisplayOn;
        set => Edit(ref _keepDisplayOn, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => Edit(ref _startWithWindows, value);
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set => Edit(ref _startMinimized, value);
    }

    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set => Edit(ref _notificationsEnabled, value);
    }

    public bool CheckForUpdates
    {
        get => _checkForUpdates;
        set => Edit(ref _checkForUpdates, value);
    }

    public bool SaveLogsToFile
    {
        get => _saveLogsToFile;
        set => Edit(ref _saveLogsToFile, value);
    }

    public AppTheme Theme
    {
        get => _theme;
        set
        {
            if (Edit(ref _theme, value))
            {
                OnPropertyChanged(nameof(ThemeIndex));
            }
        }
    }

    public UiLanguage Language
    {
        get => _language;
        set
        {
            if (Edit(ref _language, value))
            {
                OnPropertyChanged(nameof(LanguageIndex));
            }
        }
    }

    /// <summary>Gets or sets <see cref="Theme"/> as a combo-box index.</summary>
    public int ThemeIndex
    {
        get => (int)_theme;
        set
        {
            if (Enum.IsDefined((AppTheme)value))
            {
                Theme = (AppTheme)value;
            }
        }
    }

    /// <summary>Gets or sets <see cref="Language"/> as a combo-box index.</summary>
    public int LanguageIndex
    {
        get => (int)_language;
        set
        {
            if (Enum.IsDefined((UiLanguage)value))
            {
                Language = (UiLanguage)value;
            }
        }
    }

    public string? IdleError => _idleError;

    public string? CheckError => _checkError;

    public string? MinError => _minError;

    public string? MaxError => _maxError;

    public string? AmplitudeError => _amplitudeError;

    public string? RetentionError => _retentionError;

    public string? HotkeyError => _hotkeyError;

    public bool HasErrors =>
        _idleError is not null || _checkError is not null || _minError is not null || _maxError is not null
        || _amplitudeError is not null || _retentionError is not null || _hotkeyError is not null;

    public bool IsDirty => _isDirty;

    public string? HotkeyWarning => _settings.IsHotkeyRegistered ? null : L.SettingsHotkeyUnavailable;

    public string? StatusMessage => _status switch
    {
        StatusKind.Saved => L.SettingsSaved,
        StatusKind.NotWritten => L.SettingsNotWritten,
        StatusKind.StartupFailed => L.SettingsStartupFailed,
        StatusKind.HotkeyFailed => L.SettingsHotkeyUnavailable,
        _ => null,
    };

    /// <summary>Sets the hotkey text from a key press in the capture box (key is A–Z, 0–9 or F1–F24).</summary>
    public void CaptureHotkey(HotkeyModifiers modifiers, string key) =>
        HotkeyText = new HotkeyGesture(modifiers, key).ToString();

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        L.PropertyChanged -= OnLocalizerChanged;
    }

    private static string FormatSeconds(TimeSpan value, CultureInfo culture) =>
        ((int)value.TotalSeconds).ToString(culture);

    private void Save()
    {
        if (HasErrors || !TryBuild(out var built))
        {
            return;
        }

        SettingsSaveResult result;
        _saving = true;
        try
        {
            result = _settings.Save(built);
        }
        finally
        {
            _saving = false;
        }

        _status = !result.Saved ? StatusKind.NotWritten
            : !result.StartupApplied ? StatusKind.StartupFailed
            : !result.HotkeyRegistered ? StatusKind.HotkeyFailed
            : StatusKind.Saved;

        // Show the canonical form of what was stored.
        LoadFrom(built);
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void RestoreDefaults()
    {
        LoadFrom(AppSettings.Default);
        _status = StatusKind.None;
        OnPropertyChanged(nameof(StatusMessage));
        SetDirty(true);
    }

    private void OnSettingsChanged(object? sender, AppSettings current)
    {
        OnPropertyChanged(nameof(HotkeyWarning));
        if (_saving || _isDirty)
        {
            return;
        }

        // Reload without touching the status message of a save the user just made.
        LoadFrom(current);
    }

    private void OnLocalizerChanged(object? sender, PropertyChangedEventArgs e)
    {
        Revalidate();
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(HotkeyWarning));
    }

    private void LoadFrom(AppSettings s)
    {
        var culture = L.Culture;
        _loading = true;
        try
        {
            IdleText = FormatSeconds(s.Engine.IdleThreshold, culture);
            CheckText = FormatSeconds(s.Engine.CheckInterval, culture);
            MinText = FormatSeconds(s.Engine.MovementIntervalMin, culture);
            MaxText = FormatSeconds(s.Engine.MovementIntervalMax, culture);
            AmplitudeText = s.Engine.AmplitudePixels.ToString(culture);
            RetentionText = s.LogRetentionDays.ToString(culture);
            HotkeyText = s.PauseHotkey;
            KeepDisplayOn = s.Engine.KeepDisplayOn;
            StartWithWindows = s.StartWithWindows;
            StartMinimized = s.StartMinimized;
            NotificationsEnabled = s.NotificationsEnabled;
            CheckForUpdates = s.CheckForUpdates;
            SaveLogsToFile = s.SaveLogsToFile;
            Theme = s.Theme;
            Language = s.Language;
        }
        finally
        {
            _loading = false;
        }

        SetDirty(false);
        Revalidate();
        OnPropertyChanged(nameof(HotkeyWarning));
    }

    private bool Edit<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        if (!_loading)
        {
            _status = StatusKind.None;
            OnPropertyChanged(nameof(StatusMessage));
            SetDirty(true);
            Revalidate();
        }

        return true;
    }

    private void SetDirty(bool dirty)
    {
        if (_isDirty != dirty)
        {
            _isDirty = dirty;
            OnPropertyChanged(nameof(IsDirty));
        }

        _saveCommand.NotifyCanExecuteChanged();
    }

    private bool TryParse(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, L.Culture, out value);

    private void Revalidate()
    {
        var errors = ValidateEngine();

        string? retention = null;
        if (!TryParse(RetentionText, out var days))
        {
            retention = L.SettingsErrorNotNumber;
        }
        else if (days is < AppSettings.MinLogRetentionDays or > AppSettings.MaxLogRetentionDays)
        {
            retention = L.SettingsErrorRange(AppSettings.MinLogRetentionDays, AppSettings.MaxLogRetentionDays);
        }

        var hotkey = HotkeyGesture.TryParse(HotkeyText, out _) ? null : L.SettingsErrorHotkey;

        Set(ref _idleError, errors.GetValueOrDefault(SettingsField.IdleThreshold), nameof(IdleError));
        Set(ref _checkError, errors.GetValueOrDefault(SettingsField.CheckInterval), nameof(CheckError));
        Set(ref _minError, errors.GetValueOrDefault(SettingsField.MovementIntervalMin), nameof(MinError));
        Set(ref _maxError, errors.GetValueOrDefault(SettingsField.MovementIntervalMax), nameof(MaxError));
        Set(ref _amplitudeError, errors.GetValueOrDefault(SettingsField.Amplitude), nameof(AmplitudeError));
        Set(ref _retentionError, retention, nameof(RetentionError));
        Set(ref _hotkeyError, hotkey, nameof(HotkeyError));

        OnPropertyChanged(nameof(HasErrors));
        _saveCommand.NotifyCanExecuteChanged();
    }

    private void Set(ref string? field, string? value, string name)
    {
        if (field != value)
        {
            field = value;
            OnPropertyChanged(name);
        }
    }

    private Dictionary<SettingsField, string> ValidateEngine()
    {
        var errors = new Dictionary<SettingsField, string>();
        var defaults = EngineSettings.Default;
        (SettingsField Field, string Text, int Fallback)[] fields =
        [
            (SettingsField.IdleThreshold, IdleText, (int)defaults.IdleThreshold.TotalSeconds),
            (SettingsField.CheckInterval, CheckText, (int)defaults.CheckInterval.TotalSeconds),
            (SettingsField.MovementIntervalMin, MinText, (int)defaults.MovementIntervalMin.TotalSeconds),
            (SettingsField.MovementIntervalMax, MaxText, (int)defaults.MovementIntervalMax.TotalSeconds),
            (SettingsField.Amplitude, AmplitudeText, defaults.AmplitudePixels),
        ];

        var values = new int[fields.Length];
        var unparsed = new HashSet<SettingsField>();
        for (var i = 0; i < fields.Length; i++)
        {
            if (TryParse(fields[i].Text, out values[i]))
            {
                continue;
            }

            // A placeholder stands in for the field so the others are still validated.
            values[i] = fields[i].Fallback;
            unparsed.Add(fields[i].Field);
            errors[fields[i].Field] = L.SettingsErrorNotNumber;
        }

        var result = EngineSettings.Create(values[0], values[1], values[2], values[3], values[4], KeepDisplayOn);
        var rangeUnknown = unparsed.Contains(SettingsField.MovementIntervalMin)
            || unparsed.Contains(SettingsField.MovementIntervalMax);
        foreach (var error in result.Errors)
        {
            if (unparsed.Contains(error.Field)
                || (error.Code == SettingsErrorCode.MinGreaterThanMax && rangeUnknown))
            {
                continue;
            }

            if (error.Code == SettingsErrorCode.MinGreaterThanMax)
            {
                errors[error.Field] = L.SettingsErrorMinGreaterThanMax;
            }
            else
            {
                var (min, max) = EngineRanges[error.Field];
                errors[error.Field] = L.SettingsErrorRange(min, max);
            }
        }

        return errors;
    }

    private bool TryBuild(out AppSettings built)
    {
        built = AppSettings.Default;
        if (!TryParse(IdleText, out var idle) || !TryParse(CheckText, out var check)
            || !TryParse(MinText, out var min) || !TryParse(MaxText, out var max)
            || !TryParse(AmplitudeText, out var amplitude) || !TryParse(RetentionText, out var retention)
            || !HotkeyGesture.TryParse(HotkeyText, out var gesture))
        {
            return false;
        }

        var engine = EngineSettings.Create(idle, check, min, max, amplitude, KeepDisplayOn);
        if (!engine.IsValid
            || retention is < AppSettings.MinLogRetentionDays or > AppSettings.MaxLogRetentionDays)
        {
            return false;
        }

        built = AppSettings.Default with
        {
            Engine = engine.Value,
            StartWithWindows = StartWithWindows,
            StartMinimized = StartMinimized,
            NotificationsEnabled = NotificationsEnabled,
            PauseHotkey = gesture.ToString(),
            CheckForUpdates = CheckForUpdates,
            Theme = Theme,
            Language = Language,
            SaveLogsToFile = SaveLogsToFile,
            LogRetentionDays = retention,
            TrayHintShown = _settings.Current.TrayHintShown,
        };
        return true;
    }
}
