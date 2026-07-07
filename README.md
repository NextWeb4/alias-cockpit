# Alias Cockpit

Windows local email alias cockpit for generating, managing, syncing, importing, and exporting email aliases.

Current status: research-backed WinUI/.NET skeleton with a local Gmail/Outlook email alias expander as the main screen, saved input email history, per-alias site/purpose/color markers, marked/unmarked alias filtering, clickable creator links, hard-coded creator information, core alias generation, provider capability/account modeling, CSV import/export dry-run, local SQLite persistence, SQLite audit/tombstone persistence, Windows Credential Manager secret storage, SimpleLogin/addy.io mock provider adapters, SimpleLogin/addy.io HTTP adapter foundations, MSI/setup EXE packaging, unit/stress/infrastructure tests, and a benchmark entrypoint. Encrypted sync, advanced provider sync, and full UI automation are not complete yet.

The app icon source is `src\AliasCockpit.App\Assets\AppIcon.ico`. It is embedded into the app executable through `ApplicationIcon`, used by the WinUI window through `AppWindow.SetIcon`, and written into the MSI Start Menu shortcut / Add-Remove Programs entry by `scripts\package-msi.ps1`.

## Commands

```powershell
.\.tools\dotnet\dotnet.exe build AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe run --project benchmarks\AliasCockpit.Benchmarks\AliasCockpit.Benchmarks.csproj -c Release
.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verify-no-changes --verbosity minimal
.\.tools\dotnet\dotnet.exe publish src\AliasCockpit.App\AliasCockpit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\prune-publish.ps1 -PublishDir src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\clean-build-cache.ps1 -Artifacts
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-msi.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-setup-exe.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-github-release.ps1
```

The solution is built with the local .NET 10 SDK because `AliasCockpit.slnx` requires the newer SDK, while product projects target .NET 8 for Windows App SDK/WinRT runtime compatibility. The local `.tools\dotnet` runtime set includes .NET 8 and .NET 10.

Full release verification, including publish pruning, portable zip rebuild, MSI rebuild/validation, setup EXE rebuild/extract check, zip content check, process launch smoke tests, and a basic UI smoke that enters a Gmail address and copies generated aliases:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-release.ps1
```

Desktop app run candidate:

```powershell
.\.tools\dotnet\dotnet.exe run --project src\AliasCockpit.App\AliasCockpit.App.csproj
```

Published x64 app executable:

```text
src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\AliasCockpit.App.exe
```

This is the application binary inside a folder publish, not an installer package and not a single-file bundle. Run it only from the `publish` directory so the adjacent WinUI/.NET runtime files are available. Release verification prunes unused optional WinAppSDK AI/WebView/Widgets/diagnostic files before packaging, then smoke-tests the app.

Portable artifact:

```text
artifacts\AliasCockpit-win-x64-portable.zip
```

Extract the zip and run `AliasCockpit.App.exe` from the extracted folder. The zip contains the full publish directory, including the adjacent WinUI/.NET runtime files.

MSI installer artifact:

```text
artifacts\AliasCockpit-win-x64.msi
```

The MSI is generated from the full publish directory by `scripts\package-msi.ps1`. It uses the WiX CLI restored under `.tools\wix` as a build-only tool and does not add a product runtime dependency. Installing the MSI is per-machine and may require administrator elevation. Release verification validates the MSI database with `wix msi validate`.

Setup EXE installer artifact:

```text
artifacts\AliasCockpit-win-x64-setup.exe
```

This EXE is a WiX Burn installer that embeds `artifacts\AliasCockpit-win-x64.msi`. It is the EXE installer package to distribute; do not substitute the folder-publish `AliasCockpit.App.exe` or a shortcut for this artifact. Release verification extracts the setup EXE and checks that the embedded payload matches the MSI size.

GitHub release publishing target:

```text
https://github.com/NextWeb4/alias-cockpit
```

`scripts\publish-github-release.ps1` creates/uses that repository, pushes `main`, creates/updates tag release `v1.0.0`, and uploads the setup EXE, MSI, and portable zip. It expects `GITHUB_TOKEN`, `GH_TOKEN`, or the Codex GitHub integration token helper to be available at runtime; the token is not stored in this repository.

## Creator

- Name: HaoXiang Hwang
- Website: https://nextweb4.github.io/
- Email: didadida1688@gmail.com

The website and email are rendered as clickable links in the app. The email link uses `mailto:`.

## Structure

- `src/AliasCockpit.App`: WinUI 3 Windows desktop shell; current main screen is the local Email Alias Expander.
- `src/AliasCockpit.Core`: UI-free domain, generation, email alias expansion, audit, and provider abstractions.
- `src/AliasCockpit.Infrastructure`: SQLite persistence and infrastructure adapters.
- `tests/AliasCockpit.App.Tests`: App/ViewModel unit tests.
- `tests/AliasCockpit.Core.Tests`: unit and stress tests for core behavior.
- `tests/AliasCockpit.Infrastructure.Tests`: SQLite integration tests.
- `benchmarks/AliasCockpit.Benchmarks`: repeatable baseline performance checks.
- `docs`: research, architecture, and security decisions.

## Local Data

The current Email Alias Expander screen generates results locally and reads/writes only local SQLite metadata for:

- saved input email addresses;
- generated alias markers: used-at site, purpose, and color.

The SQLite database path is:

```text
%LocalAppData%\AliasCockpit\aliases.sqlite
```

This is a development database and is not encrypted yet. Provider tokens and secrets are not stored in SQLite. Do not put API tokens, passwords, recovery keys, or other secrets into marker fields such as site/purpose/tags.

Provider tokens should use Windows Credential Manager through `WindowsCredentialManagerSecretStore`; SQLite should store only future `secret_ref` values.

Provider account metadata is stored through `SqliteProviderAccountRepository`. The SimpleLogin and addy.io HTTP adapters can validate API keys and create, disable, or delete aliases when explicitly used. App startup does not call real provider APIs by default.

Audit events and tombstones are persisted through `SqliteAuditLogRepository`. Audit summaries must be redacted before append.

Batch provider disable/delete must be planned through `ProviderBatchOperationPlanner` before execution. `ProviderBatchOperationExecutor` rejects blocked plans and delete plans without explicit confirmation.

## Current Gates

- Build: passing.
- Unit/stress tests: passing.
- Benchmark: passing.
- Format check: passing with a non-blocking workspace-load warning from `dotnet format`.
- Publish: passing for `win-x64`; trimming is disabled for safer WinUI folder publish, and `scripts\prune-publish.ps1` removes only audited unused publish files.
- Launch smoke test: passing for the published exe.
- Portable artifact: passing; zip contains `AliasCockpit.App.exe` and the artifact folder launch smoke test passed.
- MSI artifact: generated by `scripts\package-msi.ps1`; full release verification rebuilds it unless `-SkipMsi` is supplied.
- Setup EXE artifact: generated by `scripts\package-setup-exe.ps1`; full release verification rebuilds and extracts it unless `-SkipSetupExe` is supplied.
- UI smoke: passing; `scripts\verify-ui-smoke.ps1` enters a Gmail address through the UI and verifies copied generated aliases.
- Release verification script: `scripts\verify-release.ps1`.
