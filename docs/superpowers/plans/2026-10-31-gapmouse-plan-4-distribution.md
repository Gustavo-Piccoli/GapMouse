# GapMouse Plan 4 — Distribution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship GapMouse to users: a per-user Inno Setup installer and a portable zip for x64 and ARM64, `SHA256SUMS.txt`, an automated GitHub Release pipeline driven by release-please, automated winget updates, and final user-facing docs.

**Architecture:** Packaging is plain PowerShell under `build/` (one script per artifact, plus verification scripts that throw on failure and double as the tests). Two workflows sit on top: `release.yml`, which builds, smoke-tests and packages both architectures and either publishes a release or, on pull requests, runs as a dry run; and `release-please.yml`, which maintains the release PR and calls `release.yml` when a release is cut. No app code changes.

**Tech Stack:**
- Packaging: PowerShell scripts that run on both Windows PowerShell 5.1 (local) and pwsh 7 (CI); Inno Setup (`ISCC.exe`).
- Workflows: GitHub Actions on `windows-latest` and `windows-11-arm`, release-please (`googleapis/release-please-action`), `wingetcreate`.
- Unchanged: .NET 10 Native AOT publish of `src/GapMouse.App`.

**Spec:** `docs/superpowers/specs/2026-10-31-gapmouse-design.md` (§6 Distribuição, §8 CI/CD, §10 item 6–7, §11.9–11.11)

## Global Constraints

- **Artifact names (spec §6), exactly:**
  - `GapMouse-Setup-<version>-<arch>.exe`
  - `GapMouse-<version>-<arch>-portable.zip`
  - `SHA256SUMS.txt`
  - `<arch>` is `x64` or `arm64`; `<version>` has no leading `v`.
- **Installer:**
  - Per user, no admin: Inno Setup, `PrivilegesRequired=lowest`.
  - Installs to `%LocalAppData%\Programs\GapMouse` (`{autopf}\GapMouse` in non-admin mode), with a Start Menu shortcut.
  - Listed in "Installed apps".
  - Has a "Start with Windows" checkbox.
- **Uninstall:** removes app files and the Run value, and keeps settings in `%AppData%` (and logs in `%LocalAppData%\GapMouse\logs`).
- **Run value:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value name `GapMouse`. The data must be byte-for-byte what `StartupCommand.For(exePath)` produces: `"<dir>\GapMouse.exe" --minimized`.
- **Portable zip:** contains the publish files plus an empty file named `portable` (no extension) at the zip root. That marker is what `AppPathsResolver` checks.
- **Installed copy is never portable:** the installer must never install a `portable` file or a `GapMouse.settings.json`.
- **Publish output:** only `GapMouse.exe` and the native `*.dll` files are shipped (today `av_libglesv2.dll`, `libHarfBuzzSharp.dll`, `libSkiaSharp.dll`). No `.pdb`. `LICENSE` ships as `LICENSE.txt` next to the exe (MIT notice), in both the zip and the installer.
- **Version:**
  - SemVer `^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$`, derived from the tag (`v1.2.3` → `1.2.3`).
  - Stamped into the exe with `-p:Version=<version>`.
  - Dry runs use `0.0.0-ci.<run_number>`.
- **ARM64:** built natively on the `windows-11-arm` hosted runner. It is part of every release, not best effort (§11.10).
- **Signing:** none in v1. SignPath comes after v1.0 (§6).
- **Winget ID:** `GustavoPiccoli.GapMouse`.
  - The first submission is manual, by Gustavo, after v1.0.
  - Later updates are automated with `wingetcreate` using the `WINGET_TOKEN` secret, and skipped when the secret is absent (§6, §11.11).
- **No network or app behavior change.** No binaries are committed (`artifacts/` is git-ignored; scripts default their output there).
- **Repo conventions:**
  - Code, scripts, commits and README in English; Conventional Commits; no AI attribution trailers.
  - CLAUDE.md and the spec stay in Portuguese. ADRs follow the existing English ADR style.
- **Scripts:**
  - Each starts with `[CmdletBinding()] param(...)`, `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'`.
  - They must run on Windows PowerShell 5.1 and pwsh 7.
  - Text files are written as UTF-8 without BOM via `[IO.File]::WriteAllText(path, text, [Text.UTF8Encoding]::new($false))`.
- **All subagents run on model sonnet.**

## Review Focus

1. **Uninstalling must not delete a Run value that belongs to another copy**, such as a portable GapMouse elsewhere. Only a value pointing at this install's exe is removed. Covered by Task 3, Test-Installer phase 2.
2. **The installer's Run value must equal the app's own format exactly.** Otherwise the app rewrites it on start, and an upgrade can leave a stale path. Covered by Task 3, Test-Installer phase 1 exact-string assertion.
3. **The installed copy must not behave as portable, and the zip must.**
   - Installed: no `portable` or `GapMouse.settings.json` in `{app}` (Task 3).
   - Zip: has the marker (Task 1, `Test-Package.ps1`).
4. **The AOT exe crashes at startup on one architecture** (trimming, missing native DLL). Each arch's publish is launched with `--minimized` and must log `START` (Task 1 `Test-Portable.ps1`, run per arch in Task 2).
5. **The installer runs while GapMouse is running.** The silent install must fail (non-zero exit) instead of half-updating, and the running exe must stay intact. Covered by Task 3, Test-Installer phase 3, which uses `AppMutex=GapMouse.SingleInstance`.

---

## File Structure

| Path | Responsibility |
|---|---|
| `build/Publish-App.ps1` | AOT publish one runtime with a stamped version; copy only the shippable files |
| `build/New-PortableZip.ps1` | Build the portable zip (publish files + `LICENSE.txt` + `portable` marker) |
| `build/New-Checksums.ps1` | Write `SHA256SUMS.txt` for the `GapMouse-*` artifacts in a directory |
| `build/Test-Portable.ps1` | Smoke test: extract the zip, launch the exe, require a `START` log line |
| `build/Test-Package.ps1` | Verify artifact names, zip contents and checksums for a version |
| `installer/GapMouse.iss` | Inno Setup script |
| `build/New-Installer.ps1` | Locate `ISCC.exe` and compile the installer for one arch |
| `build/Test-Installer.ps1` | Silent install/uninstall smoke test (registry, files, running-app refusal) |
| `.github/workflows/release.yml` | Build → smoke → package → publish (or dry run on PRs) |
| `.github/workflows/release-please.yml`, `release-please-config.json`, `.release-please-manifest.json` | Release PR, CHANGELOG and tag automation |
| `docs/RELEASING.md`, `docs/adr/0007-distribution-and-release-pipeline.md`, `CONTRIBUTING.md`, `README.md`, `CLAUDE.md` | Docs |

---

### Task 1: Publish, portable zip, checksums and their verification scripts

**Files:**
- Create: `build/Publish-App.ps1`, `build/New-PortableZip.ps1`, `build/New-Checksums.ps1`, `build/Test-Portable.ps1`, `build/Test-Package.ps1`

**Interfaces:**
- Produces (later tasks call these exact parameters):
  - `build/Publish-App.ps1 -Runtime <win-x64|win-arm64> -Version <semver> [-OutputDir <dir>]`
    - Default `-OutputDir`: `artifacts/publish/<arch>`.
    - Writes `GapMouse.exe`, `*.dll` and `LICENSE.txt` into OutputDir, which is emptied first.
    - Outputs the OutputDir path.
  - `build/New-PortableZip.ps1 -PublishDir <dir> -Version <semver> -Arch <x64|arm64> [-OutputDir <dir>]`
    - Default `-OutputDir`: `artifacts/dist`.
    - Outputs the zip path.
  - `build/New-Checksums.ps1 -Directory <dir>`
    - Hashes every `GapMouse-*.exe` and `GapMouse-*.zip` in the directory (not recursive).
    - Writes `<dir>/SHA256SUMS.txt`, sorted by file name ordinal. Each line is `<lowercase hex sha256><two spaces><file name>`, LF line endings, trailing LF, no BOM.
    - Outputs its path.
  - `build/Test-Portable.ps1 -ZipPath <zip> [-TimeoutSeconds 30]`: throws on failure.
  - `build/Test-Package.ps1 -Directory <dir> -Version <semver> [-Arch x64,arm64] [-RequireInstaller]`: throws on failure.

- [ ] **Step 1: Write the verification scripts first.**

  `Test-Package.ps1` asserts, for each `-Arch`:
  - `GapMouse-<v>-<arch>-portable.zip` exists.
  - The zip root contains exactly `GapMouse.exe`, `LICENSE.txt`, `portable` (0 bytes), and one or more `*.dll`.
  - The zip has no `*.pdb` and no subfolders.
  - With `-RequireInstaller`, `GapMouse-Setup-<v>-<arch>.exe` exists.
  - `SHA256SUMS.txt` exists, has exactly one line per `GapMouse-*` artifact in the directory, and every hash matches `Get-FileHash -Algorithm SHA256` (compare lowercase).
  - The file has no BOM (first bytes are not `EF BB BF`) and no `\r`.

  `Test-Portable.ps1`:
  - Fails fast with a clear message if a `GapMouse` process is already running. The single-instance mutex would make the launch exit silently.
  - Extracts the zip into a fresh temp directory.
  - Starts `GapMouse.exe --minimized` with that directory as its working directory.
  - Polls up to `-TimeoutSeconds` for a file `logs\gapmouse-*.log` containing a line that matches `\| START \|`.
  - Asserts the process is still running, then stops it (`Stop-Process -Force`) and deletes the temp directory in `finally`.
  - On timeout, it throws with whatever log text exists (or "no log file").

- [ ] **Step 2: Run them to see them fail**

  Run: `powershell -NoProfile -File build/Test-Package.ps1 -Directory artifacts/dist -Version 0.0.0-local -Arch x64`
  Expected: throws "…GapMouse-0.0.0-local-x64-portable.zip not found…".

- [ ] **Step 3: Implement `Publish-App.ps1`**

  - Validate `-Version` against the Global Constraints regex. Throw `"Invalid version '<v>'"` before building.
  - Derive arch: `win-x64` → `x64`, `win-arm64` → `arm64`; reject anything else with `ValidateSet`.
  - Run `dotnet publish src/GapMouse.App -c Release -r <rid> -p:Version=<v>` into a temp folder under `artifacts/obj-publish/<arch>`. Throw if `$LASTEXITCODE -ne 0`.
  - Copy only `GapMouse.exe` and `*.dll`, plus repo `LICENSE` as `LICENSE.txt`. Throw if `GapMouse.exe` is missing.
  - Resolve repo paths from `$PSScriptRoot\..`.
  - Local machines may lack `vswhere.exe` on PATH (the AOT linker step fails with "'vswhere.exe' não é reconhecido"). If `Get-Command vswhere` fails and `${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe` exists, prepend that folder to `$env:PATH` for the publish.

- [ ] **Step 4: Implement `New-PortableZip.ps1`**

  - Stage the files from `-PublishDir`, plus an empty `portable` file, in a temp folder.
  - Create the zip with `[IO.Compression.ZipFile]::CreateFromDirectory(staging, zip, Optimal, $false)` after `Add-Type -AssemblyName System.IO.Compression.FileSystem`. Overwrite an existing zip.
  - Validate `-Version` with the same regex.

- [ ] **Step 5: Implement `New-Checksums.ps1`** as specified in Interfaces.

- [ ] **Step 6: Verify end to end on x64**

  Run (Windows PowerShell 5.1):

  ```powershell
  powershell -NoProfile -File build/Publish-App.ps1 -Runtime win-x64 -Version 0.0.0-local
  powershell -NoProfile -File build/New-PortableZip.ps1 -PublishDir artifacts/publish/x64 -Version 0.0.0-local -Arch x64
  powershell -NoProfile -File build/New-Checksums.ps1 -Directory artifacts/dist
  powershell -NoProfile -File build/Test-Package.ps1 -Directory artifacts/dist -Version 0.0.0-local -Arch x64
  powershell -NoProfile -File build/Test-Portable.ps1 -ZipPath artifacts/dist/GapMouse-0.0.0-local-x64-portable.zip
  ```

  Expected: all exit 0. Also check that `(Get-Item artifacts/publish/x64/GapMouse.exe).VersionInfo.ProductVersion` starts with `0.0.0-local`.

  Run: `powershell -NoProfile -File build/Publish-App.ps1 -Runtime win-x64 -Version 1.0`
  Expected: throws `Invalid version '1.0'` without running `dotnet publish`.

  Run: `git status --short`
  Expected: nothing under `artifacts/`, because it is ignored.

- [ ] **Step 7: Commit**

  `git add build/ && git commit -m "build: add publish, portable zip and checksum scripts with verification"`

---

### Task 2: Release workflow (dry run on PRs) and release-please

**Files:**
- Create: `.github/workflows/release.yml`, `.github/workflows/release-please.yml`, `release-please-config.json`, `.release-please-manifest.json`
- Modify: `Directory.Build.props` (only if the chosen release-please updater needs an annotation on `<Version>`)

**Interfaces:**
- Consumes: the Task 1 scripts with their exact parameters.
- Produces:
  - `release.yml` jobs `version`, `build` (matrix `arch: x64|arm64`) and `package`, then `publish` (skipped on dry runs). Task 3 adds the `installer` and `installer-test` jobs between `build` and `package`. Task 4 adds `winget` after `publish`.
  - Job output `version.outputs.version` (semver, no `v`), `version.outputs.tag`, and `version.outputs.dry_run` (`'true'`/`'false'`).
  - Artifact names:
    - `publish-<arch>`: the publish dir.
    - `portable-<arch>`: the zip.
    - `dist`: the final set, uploaded by `package`.

- [ ] **Step 1: Write `release.yml`.**

  **Triggers:**
  - `push: tags: ['v*']`
  - `workflow_dispatch` (input `tag`, string, optional; empty → dry run)
  - `workflow_call` (input `tag`, string, required; secret `WINGET_TOKEN`, not required)
  - `pull_request` with `paths`: `build/**`, `installer/**`, `.github/workflows/release.yml`, `Directory.Packages.props`, `src/GapMouse.App/GapMouse.App.csproj`

  **Settings:**
  - Top-level `permissions: contents: read`.
  - `concurrency: { group: release-${{ github.ref }}, cancel-in-progress: ${{ github.event_name == 'pull_request' }} }`.

  **Jobs:**
  - **`version`** (ubuntu-latest is fine):
    - For a tag push, the tag is `github.ref_name`; for call or dispatch it is `inputs.tag`.
    - `dry_run` is `true` for `pull_request` and for a dispatch with an empty tag; the version is then `0.0.0-ci.${{ github.run_number }}`.
    - Otherwise strip the leading `v`, validate against the SemVer regex and fail on mismatch.
  - **`build`:**
    - Matrix `include`: `{arch: x64, rid: win-x64, runner: windows-latest}` and `{arch: arm64, rid: win-arm64, runner: windows-11-arm}`; `fail-fast: false`.
    - Steps: checkout and setup-dotnet with `global-json-file: global.json`.
    - Then run with `shell: pwsh`: `Publish-App.ps1`, then `New-PortableZip.ps1`, then `Test-Portable.ps1` on the zip.
    - Upload `publish-<arch>` and `portable-<arch>`.
  - **`package`** (windows-latest, `needs: [version, build]`):
    - Download `portable-*` into `artifacts/dist` (`merge-multiple: true`).
    - Run `New-Checksums.ps1` and `Test-Package.ps1 -Arch x64,arm64`.
    - Upload `dist`.
  - **`publish`** (`if: needs.version.outputs.dry_run == 'false'`, `permissions: contents: write`, `needs: [version, package]`):
    - Download `dist`.
    - If `gh release view <tag>` succeeds, run `gh release upload <tag> <files> --clobber`.
    - Otherwise run `gh release create <tag> <files> --title "GapMouse <version>" --generate-notes --verify-tag`.
    - Use `GH_TOKEN: ${{ github.token }}`.

  **Action versions:** match `ci.yml`: `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v7`. Use `actions/download-artifact@v8` (latest major as of writing).

- [ ] **Step 2: Write the release-please files.**

  - **`release-please-config.json`:**
    - One package `"."`, `"release-type": "simple"`, `"package-name": "GapMouse"`, `"include-v-in-tag": true`, `"changelog-path": "CHANGELOG.md"`.
    - Add an `extra-files` entry that keeps `<Version>` in `Directory.Build.props` in sync. Use the `xml` updater with xpath `//Project/PropertyGroup/Version`; if that is not supported, use the `generic` updater with an `<!-- x-release-please-version -->` annotation on that line.
    - The `simple` strategy also maintains `version.txt`. Accept it.
  - **`.release-please-manifest.json`:** `{ ".": "0.1.0" }`.
  - **`release-please.yml`:**
    - `on: push: branches: [main]`.
    - Job `release-please` with `permissions: contents: write, pull-requests: write`, using `googleapis/release-please-action@v5` with `config-file` and `manifest-file`. Expose outputs `release_created` and `tag_name`.
    - Job `release` with `needs: release-please`, `if: needs.release-please.outputs.release_created == 'true'`, `uses: ./.github/workflows/release.yml`, `with: { tag: ${{ needs.release-please.outputs.tag_name }} }`, `secrets: inherit`, `permissions: contents: write`.

- [ ] **Step 3: Validate the workflows locally.** Parse each YAML file and confirm it is well-formed. `actionlint` is not installed; do not install it. For example, in Windows PowerShell, check the files with a YAML-aware tool if one is available. Otherwise review indentation by eye and rely on the CI run in Step 5.

- [ ] **Step 4: Commit and push**

  ```bash
  git add .github/workflows/release.yml .github/workflows/release-please.yml release-please-config.json .release-please-manifest.json Directory.Build.props
  git commit -m "ci: add release workflow with PR dry runs and release-please"
  git push
  ```

  The controller has already opened a draft PR for `feat/distribution`, so the push triggers `pull_request`.

- [ ] **Step 5: Verify on CI**

  Run: `gh run list --branch feat/distribution --workflow release.yml -L 1`, then `gh run watch <id> --exit-status`.
  Expected: `version`, `build (x64)`, `build (arm64)` and `package` succeed, and `publish` is skipped. Check `gh run view <id> --log` for the `START` line found by `Test-Portable` on both arches.

  On failure: fix, commit as `fix(ci): …`, push, and watch again.

---

### Task 3: Inno Setup installer

**Files:**
- Create: `installer/GapMouse.iss`, `build/New-Installer.ps1`, `build/Test-Installer.ps1`
- Modify: `.github/workflows/release.yml` (add the `installer` and `installer-test` jobs; `package` also downloads the installers and passes `-RequireInstaller`)

**Interfaces:**
- Consumes:
  - `publish-<arch>` artifacts and the `version` job outputs (Task 2).
  - `Test-Package.ps1 -RequireInstaller` (Task 1).
- Produces:
  - `build/New-Installer.ps1 -PublishDir <dir> -Version <semver> -Arch <x64|arm64> [-OutputDir artifacts/dist]`, which outputs the setup path.
  - `build/Test-Installer.ps1 -InstallerPath <exe>`.
  - Artifacts `installer-<arch>`.

- [ ] **Step 1: Write `Test-Installer.ps1` first.**

  All phases install with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=<fresh temp dir> /LOG=<temp log>`. Wait for exit and assert the exit code. On failure, print the Inno log. Uninstall with `"<dir>\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`. The uninstaller re-launches itself from temp, so poll up to 60 s for the uninstall key to disappear rather than trusting the parent's exit.

  Uninstall key: `HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{DBDB1941-7D22-4FE9-85C8-99227AAA11FE}_is1`.

  **Phase 1** (`/TASKS=startup`):
  - Exit code 0.
  - `<dir>\GapMouse.exe` and `<dir>\LICENSE.txt` exist.
  - `<dir>\portable` and `<dir>\GapMouse.settings.json` do not exist.
  - The Run value `GapMouse` equals exactly `'"' + "$dir\GapMouse.exe" + '" --minimized'`.
  - The uninstall key exists under HKCU, and not under HKLM.
  - A Start Menu shortcut `GapMouse.lnk` exists under `[Environment]::GetFolderPath('Programs')`.
  - After uninstalling: `<dir>\GapMouse.exe` is gone, the Run value is gone, the uninstall key is gone, and the shortcut is gone.

  **Phase 2** (`/TASKS=""`, so no startup):
  - The Run value is absent after install.
  - Set the Run value to `"C:\Elsewhere\GapMouse.exe" --minimized` and uninstall; the value must remain unchanged. Then delete it in `finally`.

  **Phase 3:**
  - Install, then start `<dir>\GapMouse.exe --minimized` and wait about 3 s.
  - Run the installer again silently. It must exit non-zero, and `<dir>\GapMouse.exe`'s `LastWriteTimeUtc` must be unchanged.
  - Stop the process and uninstall.

  **Cleanup:** before starting, save any pre-existing Run value `GapMouse`; restore it in `finally`. The script must never leave the machine with a changed Run value.

- [ ] **Step 2: Write `installer/GapMouse.iss`.**

  **Defines** (passed with `/D`): `AppVersion`, `Arch` (`x64`|`arm64`), `SourceDir`, `OutputDir`. Each is guarded with `#ifndef … #error … #endif`.

  **`[Setup]`:**
  - `AppId={{DBDB1941-7D22-4FE9-85C8-99227AAA11FE}`, `AppName=GapMouse`, `AppVersion={#AppVersion}`, `AppPublisher=Gustavo Auad Piccoli`.
  - `AppPublisherURL`, `AppSupportURL` and `AppUpdatesURL` all `https://github.com/Gustavo-Piccoli/GapMouse`.
  - `DefaultDirName={autopf}\GapMouse`, `DisableProgramGroupPage=yes`.
  - `PrivilegesRequired=lowest`, with no `PrivilegesRequiredOverridesAllowed`.
  - `OutputDir={#OutputDir}`, `OutputBaseFilename=GapMouse-Setup-{#AppVersion}-{#Arch}`.
  - `SetupIconFile=..\src\GapMouse.App\Assets\Icons\gapmouse.ico`, `UninstallDisplayIcon={app}\GapMouse.exe`.
  - `Compression=lzma2/max`, `SolidCompression=yes`, `WizardStyle=modern`, `AppMutex=GapMouse.SingleInstance`.
  - `VersionInfoVersion` gets the numeric `major.minor.patch` part of AppVersion; compute it with the preprocessor, or pass a fifth define `NumericVersion` from the script.
  - Architecture: for `x64`, `ArchitecturesAllowed=x64compatible` and `ArchitecturesInstallIn64BitMode=x64compatible`. For `arm64`, both are `arm64`.

  **`[Languages]`:** `english` (`compiler:Default.isl`) and `brazilianportuguese` (`compiler:Languages\BrazilianPortuguese.isl`).

  **`[CustomMessages]`:**
  - `english.StartWithWindows=Start GapMouse with Windows`, `brazilianportuguese.StartWithWindows=Iniciar o GapMouse com o Windows`.
  - `english.AdditionalOptions=Additional options:`, `brazilianportuguese.AdditionalOptions=Opções adicionais:`.

  **`[Tasks]`:** `Name: startup; Description: {cm:StartWithWindows}; GroupDescription: {cm:AdditionalOptions}; Flags: unchecked`.

  **`[Files]`:** `{#SourceDir}\GapMouse.exe`, `{#SourceDir}\*.dll` and `{#SourceDir}\LICENSE.txt` → `{app}`, with `Flags: ignoreversion`.

  **`[Icons]`:** `{autoprograms}\GapMouse` → `{app}\GapMouse.exe`.

  **`[Registry]`:** `Root: HKCU; Subkey: Software\Microsoft\Windows\CurrentVersion\Run; ValueType: string; ValueName: GapMouse; ValueData: """{app}\GapMouse.exe"" --minimized"; Tasks: startup`. Do not use `uninsdeletevalue`; `[Code]` handles removal.

  **`[Run]`:** `{app}\GapMouse.exe` with `Flags: nowait postinstall skipifsilent`.

  **`[Code]`:** in `CurUninstallStepChanged(usUninstall)`, read the HKCU Run value `GapMouse`. Delete it only when it equals, case-insensitively, `'"' + ExpandConstant('{app}\GapMouse.exe') + '" --minimized'`.

- [ ] **Step 3: Write `New-Installer.ps1`.**

  - Validate the version and compute the numeric part.
  - Find `ISCC.exe` in this order:
    1. `$env:ISCC`;
    2. `Get-Command iscc`;
    3. `${env:ProgramFiles}\Inno Setup 7\ISCC.exe`;
    4. `${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe`;
    5. `${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe`;
    6. `$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe` and `…\Inno Setup 6\ISCC.exe`.
  - If none is found, throw a message telling the user to install Inno Setup or set `ISCC`. The script never installs anything.
  - Run `ISCC.exe /Q /DAppVersion=… /DNumericVersion=… /DArch=… /DSourceDir=<abs> /DOutputDir=<abs> installer\GapMouse.iss`. Throw on a non-zero exit.

- [ ] **Step 4: Wire into `release.yml`.**

  - **Job `installer`** (windows-latest, `needs: [version, build]`):
    - Install Inno Setup with `choco install innosetup -y --no-progress --version <pinned current 6.x or 7.x>`. Pin the version that choco currently serves and note it in a YAML comment.
    - Download `publish-x64` and `publish-arm64`.
    - Run `New-Installer.ps1` once per arch and upload `installer-<arch>`.
  - **Job `installer-test`:**
    - Matrix x64 on windows-latest and arm64 on windows-11-arm, `needs: [installer]`.
    - Download `installer-<arch>` and run `Test-Installer.ps1`.
  - **`package`:** add `installer-test` to its `needs` and download `installer-*` into `artifacts/dist` too. Then `New-Checksums` covers 4 artifacts, and `Test-Package` runs with `-RequireInstaller`.

- [ ] **Step 5: Local check of whatever can run without Inno Setup**

  Run: `powershell -NoProfile -File build/New-Installer.ps1 -PublishDir artifacts/publish/x64 -Version 0.0.0-local -Arch x64`
  Expected: on this machine (no Inno Setup) it throws the "install Inno Setup or set ISCC" message, and does not install anything.

- [ ] **Step 6: Commit, push and verify on CI**

  ```bash
  git add installer/ build/New-Installer.ps1 build/Test-Installer.ps1 .github/workflows/release.yml
  git commit -m "build: add per-user Inno Setup installer with install/uninstall smoke test"
  git push
  ```

  Then run `gh run watch <id> --exit-status` on the new `release.yml` run.
  Expected:
  - `installer`, `installer-test (x64)`, `installer-test (arm64)` and `package` all succeed; `publish` is skipped.
  - The `dist` artifact holds exactly 5 files: 2 setups, 2 zips and `SHA256SUMS.txt`.

  If phase 3 shows Inno's silent behavior with `AppMutex` differs from "exits non-zero", read the Inno log. Then assert the documented behavior and record the deviation in the report. The running exe must still be unchanged.

---

### Task 4: Winget update job and release runbook

**Files:**
- Modify: `.github/workflows/release.yml` (add job `winget`)
- Create: `docs/RELEASING.md`

**Interfaces:**
- Consumes: the `version` outputs and the `publish` job (Task 2), and the installer names (Task 3).

- [ ] **Step 1: Add job `winget`.**

  - Settings: `needs: [version, publish]`, `if: needs.version.outputs.dry_run == 'false' && !contains(needs.version.outputs.version, '-')` (stable releases only), `runs-on: windows-latest`.
  - Job env: `WINGET_TOKEN: ${{ secrets.WINGET_TOKEN }}`.
  - First step: if `env.WINGET_TOKEN` is empty, write a notice ("WINGET_TOKEN not set; skipping winget update") and set an output so the remaining steps are skipped. The job succeeds.
  - Otherwise:
    - Download `wingetcreate.exe` from `https://aka.ms/wingetcreate/latest`.
    - Run `wingetcreate update GustavoPiccoli.GapMouse --version <version> --urls "<x64 setup url>|x64" "<arm64 setup url>|arm64" --submit --token $env:WINGET_TOKEN`.
    - The URLs are `https://github.com/Gustavo-Piccoli/GapMouse/releases/download/<tag>/GapMouse-Setup-<version>-<arch>.exe`.
  - In `release-please.yml`, `secrets: inherit` already passes the secret.

- [ ] **Step 2: Write `docs/RELEASING.md` (English).** Cover:
  - How a release happens: Conventional Commits → release-please PR → merging it tags `vX.Y.Z` and creates the GitHub Release → `release.yml` attaches the 5 artifacts → `winget` job.
  - Manual tag fallback: push `vX.Y.Z`.
  - Dry runs: on PRs touching packaging, or `workflow_dispatch` with an empty tag.
  - Local packaging commands (Task 1 and Task 3 scripts), and that Inno Setup is needed locally only for the installer.
  - One-time setup:
    - Allow GitHub Actions to create pull requests (repo Settings → Actions → General). The controller enables this when the plan merges.
    - Release PRs are opened by `GITHUB_TOKEN`, so CI does not run on them. The owner merges them with an admin bypass, or closes and reopens to trigger checks (spec §11.9).
  - Gustavo's manual post-v1 steps (spec §11.11): create a classic PAT with `public_repo` and save it as the `WINGET_TOKEN` secret; make the first winget submission with `wingetcreate new <x64 url> <arm64 url>` (installer type inno, scope user) then a PR to `microsoft/winget-pkgs`; apply to the SignPath Foundation.
  - What `SHA256SUMS.txt` is and how to verify it: `Get-FileHash`, or `sha256sum -c`.

- [ ] **Step 3: Commit and push, then verify on CI**

  ```bash
  git add .github/workflows/release.yml docs/RELEASING.md
  git commit -m "ci: add winget update job and release runbook"
  git push
  ```

  Expected on the PR dry run: `winget` is skipped (dry run), and every other job is green.

---

### Task 5: Final docs (README, ADR 0007, CONTRIBUTING, CLAUDE.md)

**Files:**
- Modify: `README.md`, `CLAUDE.md`
- Create: `docs/adr/0007-distribution-and-release-pipeline.md`, `CONTRIBUTING.md`

- [ ] **Step 1: README.**
  - Replace the "Under development" banner with a short line saying releases are published on GitHub Releases.
  - Add a release badge: `https://img.shields.io/github/v/release/Gustavo-Piccoli/GapMouse`.
  - Add a **Download** section above "Build from source". Link to `https://github.com/Gustavo-Piccoli/GapMouse/releases/latest` and list the artifacts:
    - the installer: per user, no admin, optional "Start with Windows";
    - the portable zip: settings and logs next to the exe, runs from a USB drive;
    - `winget install GustavoPiccoli.GapMouse`, marked "after the first winget submission is approved";
    - `SHA256SUMS.txt`.
  - Explain which arch to pick: x64 or ARM64.
  - Add the SmartScreen note: the builds are not code-signed yet, so choose "More info → Run anyway". Signing via SignPath is planned.
  - Uninstall: through "Installed apps". It keeps settings in `%AppData%\GapMouse` and logs in `%LocalAppData%\GapMouse\logs`.
  - Startup ownership: if a settings file already exists, the app's saved "Start with Windows" choice wins over the installer checkbox on the next start.
  - Update "Using GapMouse" so it no longer says to run with `dotnet run` first: installed or portable first, `dotnet run` for developers.
  - Roadmap: mark 4 as current and add "5. Post-v1: winget submission, code signing".
  - Remove "Screenshots and a demo GIF arrive with the distribution plan." Add no placeholder images; Gustavo records them.

- [ ] **Step 2: ADR 0007**, in the same format as `docs/adr/0006-windows-shell-integration.md`. Record these decisions:
  - Inno Setup per-user rather than MSIX or WiX: no admin, no signing requirement, simple.
  - Ship the publish folder (exe + 3 native DLLs) rather than a true single file. Avalonia's Skia, HarfBuzz and ANGLE natives cannot be linked into the AOT exe. This deviates from spec §6 "um único .exe".
  - Native ARM64 builds on `windows-11-arm`.
  - Scripts double as tests, because Pester 5 is not available locally.
  - release-please plus `workflow_call`, because tags created by `GITHUB_TOKEN` do not trigger workflows.
  - Dry runs on PRs.
  - Uninstall removes only a Run value pointing at its own exe.

- [ ] **Step 3: `CONTRIBUTING.md`** (English, short): prerequisites, build and test commands (from CLAUDE.md's "Build e testes"), the Conventional Commits and branch + PR flow, `dotnet format`, how to run the packaging scripts, and a link to `docs/RELEASING.md`.

- [ ] **Step 4: CLAUDE.md** (Portuguese).
  - Update "Estado atual" to Plano 4.
  - Add the `build/` and `installer/` scripts to "Projetos e responsabilidades".
  - Add `release.yml` and `release-please.yml` to the CI line.
  - Add ADR 0007 to the ADR list.
  - Add non-obvious points:
    - local AOT publish may need `vswhere` on PATH, which `Publish-App.ps1` handles;
    - the Run value format must match `StartupCommand.For`;
    - release PRs don't run CI.
  - Also fix the outdated line "O movimento se limita ao monitor principal." It now says the movement is limited to the monitor where the cursor is at the start of each movement (spec §11.5, implemented in Plan 1).

- [ ] **Step 5: Commit, with the release-please footer that makes the first release 1.0.0**

  ```bash
  git add README.md CLAUDE.md CONTRIBUTING.md docs/adr/0007-distribution-and-release-pipeline.md
  git commit -m "docs: add download, install and release docs for v1" -m "Release-As: 1.0.0"
  ```

---

## Execution

- **Order:** Task 1 → 2 → 3 run sequentially on `feat/distribution`, because they share `release.yml` and each one's CI verification needs the previous one. Tasks 4 and 5 touch disjoint files and may run in parallel worktrees once Task 3 is complete, then get cherry-picked.
- **Controller-only steps** (never subagents):
  - Open the draft PR before Task 2.
  - After merge, enable "Allow GitHub Actions to create and approve pull requests": `gh api -X PUT repos/Gustavo-Piccoli/GapMouse/actions/permissions/workflow -f default_workflow_permissions=read -F can_approve_pull_request_reviews=true`.
  - Do **not** merge the release-please PR or push a `v*` tag. Publishing v1.0.0 is Gustavo's call, after his manual checks.
- **All implementers and reviewers run on model sonnet.**
