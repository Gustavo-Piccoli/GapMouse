namespace GapMouse.App.Localization;

public sealed partial class Localizer
{
    public string AboutVersionLabel => T("Version", "Versão");

    public string AboutLicenseLabel => T("License", "Licença");

    public string AboutLicense => T("MIT License", "Licença MIT");

    public string AboutRepositoryLabel => T("Repository", "Repositório");

    public string AboutOpenRepository => T("Open repository", "Abrir repositório");

    public string AboutSettingsFileLabel => T("Settings file", "Arquivo de configurações");

    public string AboutLogsFolderLabel => T("Logs folder", "Pasta de logs");

    public string AboutOpenSettingsFolder => T("Open settings folder", "Abrir pasta de configurações");

    public string AboutOpenLogsFolder => T("Open logs folder", "Abrir pasta de logs");

    public string AboutUpdatesLabel => T("Updates", "Atualizações");

    public string AboutCheckUpdates => T("Check for updates", "Verificar atualizações");

    public string AboutOpenRelease => T("Open release page", "Abrir página da versão");

    public string AboutChecking => T("Checking…", "Verificando…");

    public string AboutUpToDate => T("You're up to date.", "Você está na versão mais recente.");

    public string AboutNoReleases => T("No releases published yet.", "Nenhuma versão publicada ainda.");

    public string AboutCheckFailed => T("Could not check for updates.", "Não foi possível verificar atualizações.");

    public string AboutUpdateAvailable(string version) =>
        string.Format(Culture, T("Version {0} is available.", "Versão {0} disponível."), version);
}
