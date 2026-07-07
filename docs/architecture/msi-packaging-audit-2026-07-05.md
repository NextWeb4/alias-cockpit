# MSI Packaging Audit

Date: 2026-07-05

## Current Requirement

- Deliver an MSI installer artifact and an EXE installer artifact.
- The EXE artifact must be an installer package, not the folder-publish application executable and not a shortcut.
- Keep the WinUI app as a folder publish because the app depends on adjacent WinUI/.NET runtime files.
- Do not add installer-only tooling as a product runtime dependency.
- Do not change app startup behavior or introduce runtime network calls.

## Current Project Facts

- Product app: `src/AliasCockpit.App/`, WinUI 3, `net8.0-windows10.0.26100.0`.
- Existing release output: folder publish under `src/AliasCockpit.App/bin/Release/net8.0-windows10.0.26100.0/win-x64/publish`.
- Existing portable artifact: `artifacts/AliasCockpit-win-x64-portable.zip`.
- Existing gate: `scripts/verify-release.ps1` runs build, tests, benchmark, format check, publish, zip creation, process smoke, MSI validation, setup EXE extraction check, and UI smoke.
- `.tools/` is already ignored and used for local build tools.

## Candidate Audit

| Option | Source | License | Core ability | Pros | Cons | Maintenance | Fit | Conflict points | Adopt | Use |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| WiX CLI 5.0.2 | NuGet `wix` .NET tool | MS-RL on NuGet package page | Build MSI from `.wxs` authoring | Mature MSI toolchain, scriptable, no product runtime dependency, compatible with local `.tools/dotnet` | Installer authoring must be generated and maintained | Older than latest but stable | High | Build-time network is needed only when restoring missing tool | Yes | Restore to `.tools/wix`, generate temporary `.wxs`, build `artifacts/AliasCockpit-win-x64.msi` |
| WiX BAL extension 5.0.2 | WiX extension cache / NuGet extension | Same WiX 5 family licensing | Build Burn setup EXE bootstrapper | Produces a real `.exe` installer that embeds the MSI; reuses the same WiX toolchain | Requires BAL extension restore at build time | Stable for WiX 5 | High | Build-time network may be needed to restore extension | Yes | Generate `artifacts/AliasCockpit-win-x64-setup.exe` from the MSI |
| WiX CLI 7.0.0 | NuGet `wix` .NET tool | Source remains available, but OSMF/EULA applies for revenue organizations | Build MSI from `.wxs` authoring | Latest version | Adds commercial-use fee/EULA workflow risk | Current | Medium | License/compliance friction for distribution | No | Rejected to avoid unnecessary packaging compliance risk |
| WixSharp / SimpleMSI | NuGet/GitHub wrappers around WiX | Varies by package | Higher-level MSI authoring | Less XML authoring | Extra dependency on top of WiX, smaller adoption | Lower than WiX | Medium-low | More moving parts than needed | No | Not needed |
| Visual Studio Installer Projects | Visual Studio extension | Microsoft extension terms | MSI project in Visual Studio | Familiar in VS | Not reliable for headless CLI/release script | Maintained as VS extension | Low | Requires IDE/extension, conflicts with scripted release | No | Not adopted |
| MSIX packaging | Windows App SDK tooling | Microsoft tooling | Modern Windows package | Good for packaged apps | User explicitly requested MSI, not MSIX | Maintained | Low for this request | Changes deployment model and identity assumptions | No | Not adopted |

## Adopted Design

- Directly reuse: WiX CLI 5.0.2 as a build-only tool restored under `.tools/wix`.
- Directly reuse: WiX BAL extension 5.0.2 as a build-only extension for Burn setup EXE creation.
- Borrowed design: standard Windows Installer layout with per-machine install under `ProgramFiles64Folder`, embedded cabinet, Start Menu shortcut, major upgrade support, and a Burn bootstrapper that embeds the MSI.
- Not adopted: WiX 7 EULA/OSMF path, wrapper libraries, Visual Studio installer projects, MSIX.
- New modules: `scripts/package-msi.ps1`, `scripts/package-setup-exe.ps1`.
- Existing modules retained: WinUI app project, folder publish, portable zip artifact, process smoke and UI smoke.
- Existing code replaced: none.

## Conflict Check

| Check | Result |
| --- | --- |
| Existing tech stack | Compatible. MSI packaging consumes the existing folder publish; product TFM and Windows App SDK settings are unchanged. |
| Directory structure | Compatible. Scripts belong in `scripts/`; generated work files stay under ignored `artifacts/msi-work` and `artifacts/setup-work`. |
| Run mode | Compatible. App still runs from `AliasCockpit.App.exe` with adjacent files. |
| Build mode | Compatible. `scripts/verify-release.ps1` now calls MSI packaging and setup EXE packaging after publish/artifact smoke unless skip switches are supplied. |
| Database/config/permissions | No product database or config changes. MSI installs per-machine and may require admin elevation when installing. The Start Menu shortcut component uses an HKCU key path because Windows Installer ICE38/ICE43 require user-profile shortcut key paths to live under HKCU. |
| Offline/runtime boundary | Runtime remains offline. First-time WiX tool or extension restore is a build-time NuGet network action only. |
| License | WiX 5.0.2 is recorded as MS-RL on NuGet. WiX 6+/7 OSMF/EULA path is intentionally not adopted. |
| User request | Satisfies MSI installer and EXE installer output requirement. The EXE installer is `artifacts/AliasCockpit-win-x64-setup.exe`; the folder-publish app exe is not treated as the EXE installer package. |

## Verification Standard

- Run `scripts/clean-build-cache.ps1 -Artifacts` before final release packaging when a clean build is requested.
- Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-release.ps1` to rebuild, test, publish, smoke-test, create zip, create MSI, validate MSI, create setup EXE, extract setup EXE, and run UI smoke.
- The MSI must include the full publish directory, not only `AliasCockpit.App.exe`.
- The setup EXE must embed the MSI. `scripts/verify-release.ps1` proves this by extracting the Burn bundle and checking that a payload matches the MSI byte size.
- `scripts/package-msi.ps1` normalizes the MSI `File.Language` table to `0` after WiX build because several Windows App SDK localized `.mui` files contain language metadata that standard MSI ICE03 rejects.

## Sources

- NuGet package page for `wix` 5.0.2: `https://www.nuget.org/packages/wix/5.0.2`
- WiX OSMF / EULA documentation: `https://docs.firegiant.com/wix/osmf/`
