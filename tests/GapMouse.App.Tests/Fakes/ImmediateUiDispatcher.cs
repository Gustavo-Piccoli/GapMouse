using GapMouse.App.Services;

namespace GapMouse.App.Tests.Fakes;

internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}
