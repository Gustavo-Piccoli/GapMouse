using GapMouse.Application.Paths;

namespace GapMouse.Application.Tests.Paths;

public class AppPathsResolverTests
{
    private const string Exe = @"C:\Apps\GapMouse";
    private const string Roaming = @"C:\Users\u\AppData\Roaming";
    private const string Local = @"C:\Users\u\AppData\Local";

    [Fact]
    public void Resolve_PortableMarkerPresent_UsesExeFolder()
    {
        var paths = AppPathsResolver.Resolve(Exe, Roaming, Local, p => p == @"C:\Apps\GapMouse\portable");

        paths.ConfigDirectory.ShouldBe(Exe);
        paths.LogsDirectory.ShouldBe(@"C:\Apps\GapMouse\logs");
        paths.IsPortable.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_SettingsBesideExe_UsesExeFolder()
    {
        var paths = AppPathsResolver.Resolve(Exe, Roaming, Local, p => p == @"C:\Apps\GapMouse\GapMouse.settings.json");

        paths.ConfigDirectory.ShouldBe(Exe);
        paths.LogsDirectory.ShouldBe(@"C:\Apps\GapMouse\logs");
        paths.IsPortable.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_NoMarker_UsesProfileFolders()
    {
        var paths = AppPathsResolver.Resolve(Exe, Roaming, Local, _ => false);

        paths.ConfigDirectory.ShouldBe(@"C:\Users\u\AppData\Roaming\GapMouse");
        paths.LogsDirectory.ShouldBe(@"C:\Users\u\AppData\Local\GapMouse\logs");
        paths.IsPortable.ShouldBeFalse();
    }

    [Fact]
    public void SettingsFileAndStateFile_AreInConfigDirectory()
    {
        var paths = new AppPaths(@"C:\cfg", @"C:\cfg\logs", IsPortable: false);

        paths.SettingsFile.ShouldBe(@"C:\cfg\GapMouse.settings.json");
        paths.StateFile.ShouldBe(@"C:\cfg\GapMouse.state.json");
    }
}
