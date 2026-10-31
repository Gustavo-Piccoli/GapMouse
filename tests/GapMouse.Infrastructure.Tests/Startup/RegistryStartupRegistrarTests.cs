using GapMouse.Infrastructure.Startup;
using Microsoft.Win32;

namespace GapMouse.Infrastructure.Tests.Startup;

public sealed class RegistryStartupRegistrarTests : IDisposable
{
    private const string ParentPath = @"Software\GapMouse.Tests";
    private const string Command = @"""C:\x\GapMouse.exe"" --minimized";

    private readonly string _subKeyPath = ParentPath + "\\" + Guid.NewGuid().ToString("N");
    private readonly RegistryStartupRegistrar _registrar;

    public RegistryStartupRegistrarTests() => _registrar = new RegistryStartupRegistrar(_subKeyPath, "GapMouse");

    [Fact]
    public void SetThenGet_RoundTrips()
    {
        _registrar.SetCommand(Command);

        _registrar.GetCommand().ShouldBe(Command);
    }

    [Fact]
    public void Remove_DeletesValue()
    {
        _registrar.SetCommand("cmd");

        _registrar.Remove();

        _registrar.GetCommand().ShouldBeNull();
    }

    [Fact]
    public void Remove_WhenAbsent_DoesNotThrow() =>
        Should.NotThrow(_registrar.Remove);

    [Fact]
    public void Get_WhenKeyMissing_ReturnsNull() =>
        _registrar.GetCommand().ShouldBeNull();

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_subKeyPath, throwOnMissingSubKey: false);
        using var parent = Registry.CurrentUser.OpenSubKey(ParentPath);
        if (parent is { SubKeyCount: 0, ValueCount: 0 })
        {
            parent.Dispose();
            try
            {
                Registry.CurrentUser.DeleteSubKey(ParentPath, throwOnMissingSubKey: false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: another test instance may be using the parent.
            }
        }
    }
}
