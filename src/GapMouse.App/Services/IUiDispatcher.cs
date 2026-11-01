namespace GapMouse.App.Services;

/// <summary>Marshals work onto the UI thread; the only way engine or timer threads reach view-model state.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}
