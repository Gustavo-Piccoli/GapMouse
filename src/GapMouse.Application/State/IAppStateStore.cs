namespace GapMouse.Application.State;

/// <summary>Persists <see cref="AppState"/>.</summary>
public interface IAppStateStore
{
    /// <summary>Loads the state, or returns <see langword="null"/> when the file is missing or unreadable.</summary>
    AppState? Load();

    void Save(AppState state);
}
