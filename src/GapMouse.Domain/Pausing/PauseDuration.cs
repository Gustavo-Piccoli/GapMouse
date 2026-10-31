namespace GapMouse.Domain.Pausing;

/// <summary>How long the engine stays paused; a null <see cref="Length"/> means until resumed manually.</summary>
public sealed record PauseDuration
{
    private PauseDuration(TimeSpan? length) => Length = length;

    public static PauseDuration TenMinutes { get; } = new(TimeSpan.FromMinutes(10));

    public static PauseDuration ThirtyMinutes { get; } = new(TimeSpan.FromMinutes(30));

    public static PauseDuration OneHour { get; } = new(TimeSpan.FromHours(1));

    public static PauseDuration Indefinite { get; } = new((TimeSpan?)null);

    public TimeSpan? Length { get; }

    public bool IsIndefinite => Length is null;

    /// <summary>A custom pause of 1 to 1440 minutes.</summary>
    public static PauseDuration Custom(int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minutes, 1440);
        return new PauseDuration(TimeSpan.FromMinutes(minutes));
    }
}
