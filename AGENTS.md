# AGENTS.md

## 1. Project structure

- `src/AliasCockpit.App/` owns WinUI windows, pages, ViewModels, clipboard use, and desktop integration.
- `src/AliasCockpit.Core/` owns UI-free alias generation, Gmail/Outlook expansion, CSV import/export, audit models, provider contracts, and secret abstractions.
- `src/AliasCockpit.Infrastructure/` owns SQLite, Windows Credential Manager, and SimpleLogin/addy.io adapters; it may reference Core, never App.
- Tests are split across `tests/AliasCockpit.App.Tests/`, `tests/AliasCockpit.Core.Tests/`, and `tests/AliasCockpit.Infrastructure.Tests/`; performance baselines live in `benchmarks/AliasCockpit.Benchmarks/`.
- Architecture, research, release, and security decisions belong under `docs/`; build and release automation belongs under `scripts/`.

## 2. Run commands

- Verify the local toolchain with `.\.tools\dotnet\dotnet.exe --info` and `.\.tools\dotnet\dotnet.exe --list-runtimes`.
- Run the desktop app with `.\.tools\dotnet\dotnet.exe run --project src\AliasCockpit.App\AliasCockpit.App.csproj`.
- Startup must remain local and must not invoke real provider HTTP adapters automatically.

## 3. Test commands

- Run all unit, stress, ViewModel, SQLite, credential-store, and provider tests with `.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal`.
- Changes to generation, expansion, CSV, providers, secrets, or persistence require regression coverage in the matching test project.
- Credential Manager tests must use unique keys and delete test credentials in a `finally` path.

## 4. Build commands

- Build with `.\.tools\dotnet\dotnet.exe build AliasCockpit.slnx -v minimal`.
- Run benchmarks with `.\.tools\dotnet\dotnet.exe run --project benchmarks\AliasCockpit.Benchmarks\AliasCockpit.Benchmarks.csproj -c Release`.
- Publish x64 with `.\.tools\dotnet\dotnet.exe publish src\AliasCockpit.App\AliasCockpit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -v minimal`.
- Use `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-release.ps1` for distributable builds; individual MSI and setup commands are `scripts\package-msi.ps1` and `scripts\package-setup-exe.ps1`.
- Do not enable trimming, remove `Microsoft.InteractiveExperiences.Projection.dll`, change the .NET 8 product targets, or replace the audited WiX 5.0.2 toolchain without rebuilding and launch-testing the complete GUI package.

## 5. Code style

- Follow `.editorconfig`, nullable reference types, implicit usings, and the centrally managed package versions in `Directory.Packages.props`.
- Verify formatting with `.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verify-no-changes --verbosity minimal`; automatic formatting uses the same command without `--verify-no-changes`.
- `Directory.Build.props` uses `LangVersion=latest` and build-time code-style enforcement; preserve it while CommunityToolkit partial-property generation depends on it.

## 6. Module boundaries

- Keep alias expansion in `src/AliasCockpit.Core/Tools/EmailAliasExpander.cs`; UI code may collect input, display output, and copy results only.
- Core must not reference WinUI, HTTP, SQLite, or the clipboard. App must access storage through Core contracts.
- Saved inputs go through `ISavedEmailAddressRepository`; alias markers go through `IAliasRepository` and `AliasRecord`, not a UI-private sidecar file.
- Provider capabilities must be represented through `ProviderProfile`; never assume identical create/disable/delete/recover semantics across providers.
- Tokens belong in `WindowsCredentialManagerSecretStore`; `ProviderAccount` and SQLite may contain only `secret_ref` values.
- Batch disable/delete must use `ProviderBatchOperationPlanner`; delete execution requires `ProviderBatchOperationExecutor` with explicit confirmation.

## 7. Prohibited changes

- Never commit tokens, OAuth credentials, recovery keys, real mailbox samples, user databases, or release artifacts.
- Never store secrets in `%LocalAppData%\AliasCockpit\aliases.sqlite` or in alias site/purpose/tag fields.
- Do not describe mock adapters as real synchronization or make offline features depend on network access.
- Do not bypass dry-run, confirmation, or audit requirements for destructive provider operations.
- Do not introduce a dependency, provider SDK, or packaging change without documenting compatibility, license, network, size, maintenance, and rollback implications.

## 8. Completion criteria

- The solution builds, all affected tests pass, and formatting verification exits successfully.
- Changes affecting release layout also pass the applicable benchmark, folder publish, launch smoke, portable package check, MSI/setup validation, and UI smoke through `scripts\verify-release.ps1`.
- Core behavior has focused tests; UI behavior has ViewModel or UI-smoke coverage; high-volume list or generation changes have a benchmark or stress test.
- The final report states dependency, license, network-behavior, architecture, and unresolved-risk changes.

## 9. Review criteria

- Review secrets, database encryption status, log redaction, network calls, CSV handling, and provider deletion before cosmetic concerns.
- Verify App/Core/Infrastructure reference direction and ensure business rules remain directly testable.
- Check Gmail-only dot behavior, `+tag` limitations, deduplicated expansion results, marked/unmarked accessibility, and provider capability differences.
- For packaging changes, verify executable adjacency, icon propagation, publish pruning, installer payload identity, and artifact names.

## 10. Common risks

- `%LocalAppData%\AliasCockpit\aliases.sqlite` is currently unencrypted and contains sensitive relationship metadata.
- Gmail dot rules do not apply universally, and some websites reject plus addressing.
- Provider APIs can change authentication, rate limits, recipients, and deletion semantics; fake handlers are not live-account validation.
- WinUI folder publishes break when required adjacent runtime files are removed or when trimming/toolchain combinations change.
- Dense alias lists can regress search/filter responsiveness and keyboard accessibility without realistic data-volume tests.
