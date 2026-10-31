namespace GapMouse.Application.Abstractions;

/// <summary>Reports how long the user has been idle (no real keyboard or mouse input).</summary>
public interface IIdleTimeProvider
{
    /// <summary>Returns the time elapsed since the last real user input.</summary>
    TimeSpan GetIdleTime();
}
