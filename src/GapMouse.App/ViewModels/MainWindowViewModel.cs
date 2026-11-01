using GapMouse.App.Localization;

namespace GapMouse.App.ViewModels;

public sealed class MainWindowViewModel(
    Localizer l,
    HomeViewModel home,
    LogsViewModel logs,
    SettingsViewModel settings,
    AboutViewModel about) : ViewModelBase(l)
{
    public HomeViewModel Home { get; } = home;

    public LogsViewModel Logs { get; } = logs;

    public SettingsViewModel Settings { get; } = settings;

    public AboutViewModel About { get; } = about;
}
