using GapMouse.Domain.Pausing;

namespace GapMouse.App.Services;

public enum PauseOption
{
    TenMinutes,
    ThirtyMinutes,
    OneHour,
    Custom,
    Never,
}

public static class PauseOptions
{
    public static PauseDuration ToDuration(PauseOption option, int? customMinutes) => option switch
    {
        PauseOption.TenMinutes => PauseDuration.TenMinutes,
        PauseOption.ThirtyMinutes => PauseDuration.ThirtyMinutes,
        PauseOption.OneHour => PauseDuration.OneHour,
        PauseOption.Never => PauseDuration.Indefinite,
        PauseOption.Custom => PauseDuration.Custom(
            customMinutes ?? throw new ArgumentException("Custom pause needs minutes.", nameof(customMinutes))),
        _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };
}
