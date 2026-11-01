namespace GapMouse.Application.Shell;

/// <summary>The notification-area icon, its balloons and its context menu.</summary>
public interface ITrayIcon : IDisposable
{
    /// <summary>Sets the icon state and tooltip; the first call adds the icon.</summary>
    void SetState(TrayIconState state, string tooltip);

    void ShowBalloon(string title, string message, string? tag, bool silent);

    /// <summary>Shows the menu, blocks until it closes and returns the chosen item Id (null if dismissed).</summary>
    string? ShowMenu(IReadOnlyList<TrayMenuItem> items);

    event EventHandler? OpenRequested;

    event EventHandler? MenuRequested;

    /// <summary>Raised with the tag of the clicked balloon.</summary>
    event EventHandler<string?>? BalloonClicked;
}
