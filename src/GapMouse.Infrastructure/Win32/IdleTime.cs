namespace GapMouse.Infrastructure.Win32;

internal static class IdleTime
{
    /// <summary>Computes idle time from tick counts, correct across the 32-bit tick wrap-around.</summary>
    internal static TimeSpan FromTicks(uint tickNow, uint lastInputTick) =>
        TimeSpan.FromMilliseconds(unchecked(tickNow - lastInputTick));
}
