# Publish Size Audit

Date: 2026-07-06

## First-Principles Analysis

- User expectation: deliver a usable Windows app package in both setup EXE and MSI formats, with the installer no larger than necessary.
- Actual input state before pruning: the `win-x64` folder publish was about 261.67 MB. Largest files included `Microsoft.Windows.SDK.NET.dll` (57.26 MB), `onnxruntime.dll` (20.67 MB), `DirectML.dll` (17.84 MB), `Microsoft.WinUI.dll` (15.98 MB), and `Microsoft.ui.xaml.dll` (14.40 MB).
- Required invariant: the publish directory must still contain every file needed for WinUI startup, alias generation, SQLite persistence, clipboard copy, and basic UI smoke.
- Current app capability boundary: the app does not use Windows ML/AI, WebView2, Widgets, background tasks, app notifications, badge notifications, OAuth UI, or dump/debug symbol files at runtime.
- Fault domain: package size is dominated by self-contained Windows App SDK and .NET runtime files, not by product code or app assets.
- Minimum safe fix: keep self-contained WinUI folder publish, keep WiX MSI/Burn packaging, and remove only audited unused publish files after `dotnet publish`.

## Option Audit

| Option | Source | License | Core capability | Pros | Cons | Maintenance status | Fit | Conflict points | Adopt? | Adoption method |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Keep self-contained WinUI publish plus post-publish pruning | Existing project scripts, Microsoft Windows App SDK self-contained deployment docs: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps | Existing project licenses | Ships app without requiring the user to preinstall .NET or Windows App Runtime | Smallest behavior change, offline-friendly, no new dependency | Requires a verified remove list and launch/UI smoke | Microsoft-maintained deployment path | High | Removing required WinAppSDK files can break startup | Yes | `scripts\prune-publish.ps1`, called by `scripts\verify-release.ps1` after publish |
| Framework-dependent Windows App SDK deployment | Microsoft deployment overview: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview | Existing project licenses | Smaller app payload by relying on installed/shared runtime | Can reduce app folder significantly | Changes runtime prerequisite and deployment model | Microsoft-maintained | Medium | May require runtime install/bootstrapper and changes offline boundary | No | Reconsider later only with explicit runtime prerequisite strategy |
| .NET trimming / single-file publish | Microsoft single-file and trimming docs: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview and https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained | Existing project licenses | Reduces .NET payload and/or packs files | Potentially smaller output | WinUI/WinRT reflection and native assets make startup risk high; current AGENTS rules already prohibit re-enabling trimming without full GUI verification | Microsoft-maintained | Low for current release | Conflicts with known WinUI publish risk in this repo | No | Keep `PublishTrimmed=false` and `PublishSingleFile=false` |
| UPX binary compression | UPX official/GitHub: https://upx.github.io/ and https://github.com/upx/upx | GPL with stated use/distribution permissions | Compresses EXE/DLL files | Can reduce some native binary sizes | New tool dependency, AV false-positive risk, harder crash/debug analysis, must validate many native DLLs | Active open source | Low | Adds maintenance and security-review burden for a local desktop app | No | Not introduced |
| WiX MSI + Burn setup EXE | Existing project scripts, WiX docs: https://docs.firegiant.com/wix/ | WiX uses MS-RL; WiX 5 binary use also needs OSMF/EULA awareness already documented in `msi-packaging-audit-2026-07-05.md` | Builds MSI and EXE installer artifacts | Already integrated and validated | Installer size follows publish payload size | Active, already pinned to 5.0.2 | High | Do not upgrade to WiX 6/7 without re-audit | Yes, already | Keep current scripts, package the pruned publish folder |

## Adopted Scope

- Directly reused: existing `.NET publish`, WiX MSI, WiX Burn setup EXE, process smoke, and UI smoke scripts.
- Added: `scripts\prune-publish.ps1`, a guarded post-publish file remover for audited unused files.
- Rejected: new compressor dependencies, framework-dependent runtime install strategy, trimming, and single-file publish.
- Existing code retained: App/Core/Infrastructure projects, Windows App SDK 2.2.0, .NET 8 target framework, WiX 5.0.2 packaging.

## Conflict Check

| Check | Result |
| --- | --- |
| Current technology stack | Compatible; still WinUI 3/.NET 8 folder publish. |
| Directory structure | Compatible; pruning is isolated under `scripts/` and operates only on publish output. |
| Run/build/package flow | Compatible; `verify-release.ps1` now prunes after publish and before smoke/package. |
| Offline/network boundary | No runtime network change. |
| License | No new dependency, so no new license obligation. |
| User requirement | Compatible; outputs remain portable zip, MSI, and setup EXE. |
| Known removal conflict | `Microsoft.InteractiveExperiences.Projection.dll` is not removed because a probe launch failed with exit code `-1073741189`. |

## Verification

- Dry run identified 83 removable files totaling 59,330,681 bytes in the existing publish folder.
- Probe with the adopted remove list reduced the publish folder from about 261.67 MB to about 205.09 MB.
- Process launch smoke passed on the pruned probe folder.
- UI smoke passed on the pruned probe folder after making the smoke script restore, resize, and temporarily topmost the app window before coordinate fallback.

## Rollback

- Remove the `prune publish` step from `scripts\verify-release.ps1`.
- Delete or stop using `scripts\prune-publish.ps1`.
- Re-run `dotnet publish` to restore the full unpruned publish directory.
