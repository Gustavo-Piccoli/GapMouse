using GapMouse.Application.State;
using GapMouse.Domain.Engine;

namespace GapMouse.Application.Tests.State;

public class AppStateTests
{
    [Fact]
    public void TodayOrNull_SameDay_ReturnsStats()
    {
        var stats = new DailyStats(new DateOnly(2026, 10, 31), TimeSpan.FromMinutes(5), 3);

        new AppState(stats).TodayOrNull(new DateOnly(2026, 10, 31)).ShouldBe(stats);
    }

    [Fact]
    public void TodayOrNull_OtherDay_ReturnsNull()
    {
        var stats = new DailyStats(new DateOnly(2026, 10, 30), TimeSpan.FromMinutes(5), 3);

        new AppState(stats).TodayOrNull(new DateOnly(2026, 10, 31)).ShouldBeNull();
    }
}
