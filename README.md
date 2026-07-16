[English](README.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

# Alias Cockpit

A local-first Windows desktop cockpit for generating, marking, storing, importing, exporting, and optionally synchronizing email aliases.

![Last commit](https://img.shields.io/github/last-commit/NextWeb4/alias-cockpit?style=flat-square)
![Repository size](https://img.shields.io/github/repo-size/NextWeb4/alias-cockpit?style=flat-square)
![GitHub stars](https://img.shields.io/github/stars/NextWeb4/alias-cockpit?style=flat-square)
![C# and .NET 8](https://img.shields.io/badge/C%23-.NET%208-512BD4?style=flat-square&logo=dotnet&logoColor=white)

## Current Scope

Alias Cockpit is an active WinUI 3/.NET application. Its current main screen is an offline Gmail/Outlook alias expander with saved input history, site/purpose/color markers, marked/unmarked filtering, and copy actions. The repository also contains:

- core alias generation, entropy estimation, CSV import/export dry runs, audit events, tombstones, and provider capability models;
- local SQLite repositories for aliases, saved addresses, provider accounts, and audit data;
- Windows Credential Manager secret storage;
- SimpleLogin and addy.io mock adapters plus HTTP adapter foundations;
- xUnit unit, stress, ViewModel, and infrastructure tests;
- repeatable generation, CSV, and SQLite benchmarks;
- folder-publish, portable ZIP, MSI, setup EXE, and GitHub Release tooling.

Encrypted sync, advanced provider synchronization, and full UI automation are not complete. The application does not call real provider APIs during normal startup.

## Requirements

- Windows 10 version 2004 (`10.0.19041.0`) or newer.
- The repository's ignored local `.tools\dotnet` installation. Existing project documentation records a .NET 10 SDK for the `.slnx` solution and .NET 8 runtimes for the product projects.
- A desktop session for the UI smoke test.

## Run

```powershell
.\.tools\dotnet\dotnet.exe run --project src\AliasCockpit.App\AliasCockpit.App.csproj
```

The app reads and writes local metadata at:

```text
%LocalAppData%\AliasCockpit\aliases.sqlite
```

This development database is not encrypted. It may contain alias metadata, saved input addresses, marker values, audit data, and provider `secret_ref` values, but never provider tokens or API secrets.

## Build, Test, and Format

```powershell
.\.tools\dotnet\dotnet.exe build AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe run --project benchmarks\AliasCockpit.Benchmarks\AliasCockpit.Benchmarks.csproj -c Release
.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verify-no-changes --verbosity minimal
```

The repository documentation notes that `dotnet format` can emit a non-blocking workspace-load warning; use its exit code together with build and test results.

## Release Packaging

Run the complete release verification when preparing distributable files:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-release.ps1
```

That script builds and tests the solution, runs the benchmark and format gate, publishes the app, prunes an audited set of unused WinAppSDK files, rebuilds the portable ZIP/MSI/setup EXE, validates package contents, launch-smokes the published and portable executables, and runs the basic UI smoke.

Individual commands are also available:

```powershell
.\.tools\dotnet\dotnet.exe publish src\AliasCockpit.App\AliasCockpit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\prune-publish.ps1 -PublishDir src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-msi.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-setup-exe.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-github-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\clean-build-cache.ps1 -Artifacts
```

Expected artifacts:

| Artifact | Purpose |
| --- | --- |
| `src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\AliasCockpit.App.exe` | App executable inside the complete folder publish; not an installer or single-file bundle |
| `artifacts\AliasCockpit-win-x64-portable.zip` | Complete portable publish directory |
| `artifacts\AliasCockpit-win-x64.msi` | Per-machine WiX MSI; installation may require elevation |
| `artifacts\AliasCockpit-win-x64-setup.exe` | WiX Burn setup executable embedding the MSI |

Keep the published executable beside its WinUI/.NET runtime files. The release scripts use WiX only as a build tool. `scripts\publish-github-release.ps1` targets `NextWeb4/alias-cockpit`, tag `v1.0.0`, and reads credentials only at runtime from `GITHUB_TOKEN`, `GH_TOKEN`, Git Credential Manager, or the Codex integration helper.

## Project Structure

| Path | Responsibility |
| --- | --- |
| `src/AliasCockpit.App/` | WinUI 3 shell, main page, ViewModel, clipboard, and desktop integration |
| `src/AliasCockpit.Core/` | UI-free aliases, generation, CSV, audit, provider, secret, security, and expander contracts |
| `src/AliasCockpit.Infrastructure/` | SQLite, Windows Credential Manager, and provider adapters |
| `tests/AliasCockpit.App.Tests/` | ViewModel tests without launching a WinUI window |
| `tests/AliasCockpit.Core.Tests/` | Unit and stress coverage for domain behavior |
| `tests/AliasCockpit.Infrastructure.Tests/` | SQLite, credential-store, and provider adapter integration tests |
| `benchmarks/AliasCockpit.Benchmarks/` | Generation, CSV dry-run, and SQLite baselines |
| `docs/` | Research, architecture decisions, security model, and release notes |
| `scripts/` | Branding, publish, package, cleanup, release, and smoke-test automation |

## Data and Security Boundaries

- Gmail dot aliases apply only to Gmail/Googlemail addresses; Google Workspace custom domains are not assumed to share that behavior.
- `+tag` aliases are not accepted by every third-party form.
- Store provider tokens through `WindowsCredentialManagerSecretStore`; SQLite stores only references generated by the secret-key model.
- Do not put passwords, tokens, recovery codes, or other secrets in site, purpose, tag, marker, or saved-address fields.
- Provider disable/delete batches must be planned before execution. Delete requires explicit confirmation and an audit trail.
- The HTTP adapters can validate keys and perform supported alias operations when explicitly invoked, but mock adapters and fake HTTP handlers do not prove real-account end-to-end compatibility.

## Creator

- HaoXiang Huang
- [didadida1688@gmail.com](mailto:didadida1688@gmail.com)
- <https://nextweb4.github.io/>

The source icon is `src/AliasCockpit.App/Assets/AppIcon.ico`; packaging also uses it for the executable, WinUI window, Start Menu shortcut, Add/Remove Programs entry, and setup bundle.

## License

No `LICENSE` file was found in the audited repository. Unless the owner declares terms elsewhere, the repository should not be treated as granting an open-source license.
