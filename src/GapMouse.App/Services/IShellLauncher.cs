namespace GapMouse.App.Services;

public interface IShellLauncher
{
    void OpenFolder(string path);

    void OpenUrl(Uri url);
}
