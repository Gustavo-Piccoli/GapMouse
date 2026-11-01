# Releasing GapMouse

## How a release happens

1. Merge changes to `main` using Conventional Commits (`feat:`, `fix:`, ...).
2. release-please keeps a release PR open with the next version and changelog.
3. Merging that PR creates the tag `vX.Y.Z` and the GitHub Release.
4. `release.yml` builds x64 and arm64, smoke-tests the portable zips and installers, and attaches the 5 artifacts to the release:
   - `GapMouse-Setup-<version>-x64.exe` and `GapMouse-Setup-<version>-arm64.exe`
   - `GapMouse-<version>-x64-portable.zip` and `GapMouse-<version>-arm64-portable.zip`
   - `SHA256SUMS.txt`
   Note: release-please publishes the GitHub release immediately; `release.yml` attaches the assets a few minutes later (typically under 10).
5. For stable versions (no `-` suffix), the `winget` job submits a manifest update with `wingetcreate`. It skips cleanly when the `WINGET_TOKEN` secret is not set.

### Manual tag fallback

Push a tag `vX.Y.Z` (SemVer, `v` prefix) and `release.yml` runs the same pipeline. If the release already exists, the artifacts are uploaded to it (`--clobber`).

### Recovering from a failed release

If `release.yml` fails after release-please merged, the tag and an asset-less GitHub Release already exist, so the manual tag fallback does not apply. Instead, open the failed run and use "Re-run failed jobs", or run `gh workflow run release.yml -f tag=vX.Y.Z` (assets are uploaded with `--clobber`).

### Pre-releases

Tags like `vX.Y.Z-rc.N` are published as GitHub pre-releases and are skipped by the `winget` job.

### Merging PRs

Merge PRs with rebase, not squash, so commit footers such as `Release-As:` survive into the history release-please reads.

### Dry runs

A dry run builds and tests everything but publishes nothing (version `0.0.0-ci.<run_number>`). It happens on pull requests touching packaging files (`build/**`, `installer/**`, `release.yml`, `Directory.Packages.props`, `Directory.Build.props`, `global.json`, `GapMouse.App.csproj`) and on `workflow_dispatch` with an empty tag. The `publish` and `winget` jobs are skipped.

## Local packaging

From the repo root, in PowerShell (replace `<v>` with a SemVer version, for example `0.0.0-local`):

```powershell
build/Publish-App.ps1 -Runtime win-x64 -Version <v>
build/New-PortableZip.ps1 -PublishDir artifacts/publish/x64 -Version <v> -Arch x64
build/New-Installer.ps1 -PublishDir artifacts/publish/x64 -Version <v> -Arch x64
build/New-Checksums.ps1 -Directory artifacts/dist
build/Test-Package.ps1 -Directory artifacts/dist -Version <v> -Arch x64
build/Test-Portable.ps1 -ZipPath artifacts/dist/GapMouse-<v>-x64-portable.zip
```

Notes:

- Inno Setup 6 is needed locally only for `New-Installer.ps1`. Install it, or point the `ISCC` environment variable at `ISCC.exe`. CI pins Chocolatey `innosetup` 6.7.1.
- `Test-Installer.ps1 -InstallerPath <exe>` really installs and uninstalls GapMouse on the machine and touches the `HKCU` Run value (it restores it). Run it only on a machine where that is acceptable.
- Output goes under `artifacts/`, which is git-ignored.

## One-time setup

- **Allow Actions to create pull requests:** repo Settings, Actions, General, "Allow GitHub Actions to create and approve pull requests". release-please needs it to open the release PR.
- **CI on release PRs:** release PRs are opened with `GITHUB_TOKEN`, so CI does not run on them. The owner merges them with an admin bypass, or closes and reopens the PR to trigger the checks.

## Manual steps after v1 (owner)

1. Make the first winget submission by hand: run `wingetcreate new <x64 installer url> <arm64 installer url>` (installer type inno, scope user), then open a PR to `microsoft/winget-pkgs`. The package ID is `GustavoPiccoli.GapMouse`. `wingetcreate update` later needs the same number of installer URLs (2: x64 and arm64) as the existing manifest, which the `winget` job already passes.
2. Only after that first submission is merged into `microsoft/winget-pkgs`, create a classic personal access token with the `public_repo` scope and save it as the repository secret `WINGET_TOKEN`. Adding it earlier makes the `winget` job of the next stable release fail, because the package does not exist yet. Later versions are automated by the `winget` job.
3. Apply to the SignPath Foundation for free code signing. v1 is unsigned.

## Verifying downloads with SHA256SUMS.txt

`SHA256SUMS.txt` lists the SHA-256 hash of every file in the release. Download it next to the files, then:

```powershell
Get-FileHash .\GapMouse-Setup-<version>-x64.exe -Algorithm SHA256
# compare with the matching line in SHA256SUMS.txt
```

or, with a Unix-style tool (Git Bash, WSL):

```bash
sha256sum -c SHA256SUMS.txt --ignore-missing
```
