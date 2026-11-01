# 7. Distribution and release pipeline

## Context

GapMouse needs to reach users as an installer, a portable zip and (later) a winget package,
for x64 and ARM64, without admin rights and without a code-signing certificate in v1.

## Decision

- **Inno Setup, per user.** The installer uses Inno Setup 6 with `PrivilegesRequired=lowest`
  rather than MSIX or WiX. No admin prompt, no signing requirement, a short script. It
  installs to `{autopf}\GapMouse`, refuses to run while the app is open (`AppMutex`) and
  offers an unchecked "Start with Windows" task.
- **Ship the publish folder, not a true single file.** The package is `GapMouse.exe` plus
  three native DLLs (`av_libglesv2.dll`, `libHarfBuzzSharp.dll`, `libSkiaSharp.dll`).
  Avalonia's ANGLE, HarfBuzz and Skia natives cannot be linked into the AOT exe. This
  deviates from spec section 6 ("um unico .exe") on purpose.
- **Native ARM64 builds.** ARM64 is built on the `windows-11-arm` hosted runner and its
  installer is smoke-tested there; it is part of every release.
- **Scripts double as tests.** Pester 5 is not available locally, so `build/Test-*.ps1`
  verify the package, the portable zip and the silent install and uninstall. CI runs the
  same scripts developers run.
- **release-please plus `workflow_call`.** release-please opens the release PR and updates
  `<Version>` in `Directory.Build.props`. Tags and releases created with `GITHUB_TOKEN` do
  not trigger other workflows, so `release-please.yml` calls `release.yml` directly. A
  manual `v*` tag push also works.
- **Dry runs on PRs.** Pull requests that touch packaging files, and manual runs with an
  empty tag, build and test everything with version `0.0.0-ci.<run_number>` and skip the
  publish job.
- **Uninstall removes only its own Run value.** The Run value is deleted only when it
  equals this installation's `"<app>\GapMouse.exe" --minimized`, so a portable copy's or
  another install's startup entry survives. The installer writes the same string that
  `StartupCommand.For` produces, so the app does not rewrite it on start.
- **No signing in v1; winget later.** SignPath comes after v1.0. The first winget
  submission is manual; later updates use `wingetcreate` and are skipped without the
  `WINGET_TOKEN` secret.

## Consequences

- Users get an installer or a zip per architecture, plus `SHA256SUMS.txt`.
- SmartScreen warns about unsigned builds until signing is added.
- Release PRs from release-please do not run the normal CI, because of the `GITHUB_TOKEN`
  limitation.
- Packaging changes are exercised on every PR that touches them, before a real release.
