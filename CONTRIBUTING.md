# Contributing

## Prerequisites

- Windows.
- The [.NET SDK](https://dotnet.microsoft.com/download) 10.0.300 or newer (pinned in `global.json`).
- For installer builds only: [Inno Setup 6](https://jrsoftware.org/isinfo.php).

## Build and test

```powershell
dotnet build
dotnet test                                          # everything, including real Win32 tests
dotnet test --filter-not-trait "Category=Win32"      # as in CI
dotnet format --verify-no-changes
```

Win32 tests move the cursor by 1 px, so keep your hands off the mouse for a couple of seconds. Tests run on Microsoft.Testing.Platform, hence `--filter-not-trait`. Run the app with `dotnet run --project src/GapMouse.App`.

## Workflow

- Branch off `main`, open a pull request, and keep `dotnet format` and the tests passing.
- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`...). release-please builds the changelog and versions from them.
- Code, names and commits are in English. Code in `src/` must stay Native AOT compatible.

## Packaging scripts

The scripts in `build/` run on Windows PowerShell 5.1 and pwsh 7 and write to `artifacts/` (git-ignored):

```powershell
powershell -NoProfile -File build/Publish-App.ps1 -Runtime win-x64 -Version 0.0.0-local
powershell -NoProfile -File build/New-PortableZip.ps1 -PublishDir artifacts/publish/x64 -Version 0.0.0-local -Arch x64
powershell -NoProfile -File build/New-Installer.ps1 -PublishDir artifacts/publish/x64 -Version 0.0.0-local -Arch x64
powershell -NoProfile -File build/New-Checksums.ps1 -Directory artifacts/dist
powershell -NoProfile -File build/Test-Package.ps1 -Directory artifacts/dist -Version 0.0.0-local -Arch x64
powershell -NoProfile -File build/Test-Portable.ps1 -ZipPath artifacts/dist/GapMouse-0.0.0-local-x64-portable.zip
powershell -NoProfile -File build/Test-Installer.ps1 -InstallerPath artifacts/dist/GapMouse-Setup-0.0.0-local-x64.exe
```

`New-Installer.ps1` needs Inno Setup installed (it only looks for `ISCC.exe`, it never installs it). `Test-Installer.ps1` does a silent install and uninstall on your machine, touching your user profile and the Run registry key, so close GapMouse first.

## Releasing

See [docs/RELEASING.md](docs/RELEASING.md).
