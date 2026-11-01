namespace GapMouse.App.Services;

public interface IPausePrompt
{
    /// <summary>Asks for a custom pause; returns whole minutes from 1 to 1440, or null when cancelled.</summary>
    Task<int?> AskMinutesAsync();
}
