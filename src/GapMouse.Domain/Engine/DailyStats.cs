namespace GapMouse.Domain.Engine;

/// <summary>Moving time and activation count accumulated during one local day.</summary>
public readonly record struct DailyStats(DateOnly Day, TimeSpan MovingTime, int MovingActivations)
{
    public static DailyStats Empty(DateOnly day) => new(day, TimeSpan.Zero, 0);
}
