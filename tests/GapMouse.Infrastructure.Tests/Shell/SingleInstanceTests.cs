using GapMouse.Infrastructure.Shell;

namespace GapMouse.Infrastructure.Tests.Shell;

[Collection("ShellWin32")]
[Trait("Category", "Win32")]
public sealed class SingleInstanceTests
{
    private static string UniqueName() => @"Local\GapMouse.Tests." + Guid.NewGuid().ToString("N");

    [Fact]
    public void TryAcquire_First_ReturnsInstance()
    {
        using var instance = SingleInstance.TryAcquire(UniqueName());

        instance.ShouldNotBeNull();
    }

    [Fact]
    public void TryAcquire_WhileHeld_ReturnsNull()
    {
        var name = UniqueName();
        using var first = SingleInstance.TryAcquire(name);

        SingleInstance.TryAcquire(name).ShouldBeNull();
    }

    [Fact]
    public void TryAcquire_AfterDispose_Succeeds()
    {
        var name = UniqueName();
        SingleInstance.TryAcquire(name)!.Dispose();

        using var again = SingleInstance.TryAcquire(name);

        again.ShouldNotBeNull();
    }
}
