using GapMouse.App;

namespace GapMouse.App.Tests;

public sealed class StartupOptionsTests
{
    [Fact]
    public void Parse_MinimizedFlag_SetsMinimized() =>
        StartupOptions.Parse(["--minimized"]).Minimized.ShouldBeTrue();

    [Fact]
    public void Parse_UpperCaseFlag_SetsMinimized() =>
        StartupOptions.Parse(["--MINIMIZED"]).Minimized.ShouldBeTrue();

    [Fact]
    public void Parse_NoArguments_IsNotMinimized() =>
        StartupOptions.Parse([]).Minimized.ShouldBeFalse();

    [Fact]
    public void Parse_UnknownArguments_AreIgnored() =>
        StartupOptions.Parse(["--other", "x"]).Minimized.ShouldBeFalse();
}
