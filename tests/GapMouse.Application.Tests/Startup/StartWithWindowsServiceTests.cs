using System.Security;
using GapMouse.Application.Startup;
using GapMouse.Application.Tests.Fakes;
using GapMouse.Testing.Logging;
using Microsoft.Extensions.Logging;

namespace GapMouse.Application.Tests.Startup;

public class StartWithWindowsServiceTests
{
    private const string Exe = @"C:\Apps\GapMouse\GapMouse.exe";

    private readonly FakeRegistrar _registrar = new();
    private readonly ListLogger<StartWithWindowsService> _logger = new();

    [Fact]
    public void For_QuotesPathAndAddsMinimized() =>
        StartupCommand.For(Exe).ShouldBe("\"C:\\Apps\\GapMouse\\GapMouse.exe\" --minimized");

    [Fact]
    public void InitialPreference_NoSettingsFileAndRunKeyPresent_ReturnsTrue()
    {
        _registrar.Command = "anything";

        CreateService().InitialPreference(settingsFileExisted: false, savedPreference: false).ShouldBeTrue();
    }

    [Fact]
    public void InitialPreference_NoSettingsFileAndNoRunKey_ReturnsFalse() =>
        CreateService().InitialPreference(settingsFileExisted: false, savedPreference: true).ShouldBeFalse();

    [Fact]
    public void InitialPreference_SettingsFileExists_ReturnsSavedValue()
    {
        _registrar.Command = "anything";

        CreateService().InitialPreference(settingsFileExisted: true, savedPreference: false).ShouldBeFalse();
    }

    [Fact]
    public void Apply_EnabledAndMissing_WritesCommand()
    {
        CreateService().Apply(true).ShouldBeTrue();

        _registrar.Command.ShouldBe(StartupCommand.For(Exe));
        _registrar.Writes.ShouldBe(1);
    }

    [Fact]
    public void Apply_EnabledAndStalePath_RewritesCommand()
    {
        _registrar.Command = StartupCommand.For(@"D:\Old\GapMouse.exe");

        CreateService().Apply(true).ShouldBeTrue();

        _registrar.Command.ShouldBe(StartupCommand.For(Exe));
        _registrar.Writes.ShouldBe(1);
    }

    [Fact]
    public void Apply_EnabledAndCurrent_DoesNotWrite()
    {
        _registrar.Command = StartupCommand.For(Exe).ToUpperInvariant();

        CreateService().Apply(true).ShouldBeTrue();

        _registrar.Writes.ShouldBe(0);
    }

    [Fact]
    public void Apply_DisabledAndPresent_Removes()
    {
        _registrar.Command = StartupCommand.For(Exe);

        CreateService().Apply(false).ShouldBeTrue();

        _registrar.Command.ShouldBeNull();
        _registrar.Removes.ShouldBe(1);
    }

    [Fact]
    public void Apply_DisabledAndAbsent_DoesNothing()
    {
        CreateService().Apply(false).ShouldBeTrue();

        _registrar.Removes.ShouldBe(0);
        _registrar.Writes.ShouldBe(0);
    }

    [Fact]
    public void Apply_RegistrarThrowsUnauthorized_ReturnsFalseAndLogsWarning()
    {
        _registrar.ToThrow = new UnauthorizedAccessException();

        CreateService().Apply(true).ShouldBeFalse();

        _logger.Levels.ShouldContain(LogLevel.Warning);
    }

    [Fact]
    public void Apply_RegistrarThrowsSecurity_ReturnsFalseAndLogsWarning()
    {
        _registrar.ToThrow = new SecurityException();

        CreateService().Apply(false).ShouldBeFalse();

        _logger.Levels.ShouldContain(LogLevel.Warning);
    }

    [Fact]
    public void InitialPreference_NoSettingsFileAndRegistrarThrows_ReturnsFalseAndLogsWarning()
    {
        _registrar.ToThrow = new SecurityException();

        CreateService().InitialPreference(settingsFileExisted: false, savedPreference: true).ShouldBeFalse();

        _logger.Levels.ShouldContain(LogLevel.Warning);
    }

    [Fact]
    public void InitialPreference_SettingsFileExistsAndRegistrarThrows_ReturnsSavedValueWithoutReadingRegistry()
    {
        _registrar.ToThrow = new SecurityException();

        CreateService().InitialPreference(settingsFileExisted: true, savedPreference: true).ShouldBeTrue();
    }

    private StartWithWindowsService CreateService() => new(_registrar, Exe, _logger);

    private sealed class FakeRegistrar : IStartupRegistrar
    {
        public string? Command { get; set; }

        public int Writes { get; private set; }

        public int Removes { get; private set; }

        public Exception? ToThrow { get; set; }

        public string? GetCommand()
        {
            if (ToThrow is not null)
            {
                throw ToThrow;
            }

            return Command;
        }

        public void SetCommand(string command)
        {
            if (ToThrow is not null)
            {
                throw ToThrow;
            }

            Command = command;
            Writes++;
        }

        public void Remove()
        {
            if (ToThrow is not null)
            {
                throw ToThrow;
            }

            Command = null;
            Removes++;
        }
    }
}
