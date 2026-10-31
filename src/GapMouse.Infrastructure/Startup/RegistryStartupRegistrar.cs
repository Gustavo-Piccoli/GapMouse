using GapMouse.Application.Startup;
using Microsoft.Win32;

namespace GapMouse.Infrastructure.Startup;

public sealed class RegistryStartupRegistrar(
    string subKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
    string valueName = "GapMouse") : IStartupRegistrar
{
    public string? GetCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        return key?.GetValue(valueName) as string;
    }

    public void SetCommand(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        key.SetValue(valueName, command, RegistryValueKind.String);
    }

    public void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
