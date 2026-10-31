namespace GapMouse.Application.Startup;

/// <summary>Reads and writes the per-user "start with Windows" command.</summary>
public interface IStartupRegistrar
{
    string? GetCommand();

    void SetCommand(string command);

    void Remove();
}
