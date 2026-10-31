using GapMouse.Domain.Engine;

namespace GapMouse.Application.State;

/// <summary>Runtime state persisted between runs.</summary>
public sealed record AppState(DailyStats Today)
{
    /// <summary>Returns the stored stats only when they belong to <paramref name="today"/>.</summary>
    public DailyStats? TodayOrNull(DateOnly today) => Today.Day == today ? Today : null;
}
