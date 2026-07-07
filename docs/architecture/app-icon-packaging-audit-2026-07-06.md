# App Icon Packaging Audit

Date: 2026-07-06

## First-Principles Analysis

- User expectation: the generated Alias Cockpit icon should appear in Windows shell surfaces such as the taskbar, app executable, installer, Start Menu shortcut, and Add/Remove Programs.
- Actual behavior before fix: icon image assets existed under `src/AliasCockpit.App/Assets/`, and the WinUI window used `AppWindow.SetIcon("Assets/AppIcon.ico")`, but the app project did not embed the ICO as the Win32 executable icon and the MSI shortcut did not specify an icon.
- Broken invariant: installing the app must not rely on Windows shell fallback icons when a branded `AppIcon.ico` is present.
- Faulty layer: packaging metadata, not the generated image asset.
- Minimum fix: keep the existing generated ICO/PNG assets, add `ApplicationIcon` to the App project, and add WiX `Icon`, `ARPPRODUCTICON`, and Shortcut `Icon` metadata in MSI packaging.

## Scope

- Directly reused: existing `Assets\AppIcon.ico`, existing WinUI `AppWindow.SetIcon`, existing WiX MSI and Burn setup scripts.
- Added: EXE resource binding via `AliasCockpit.App.csproj`, MSI shortcut/Add-Remove Programs icon binding via `package-msi.ps1`.
- Existing setup EXE icon path retained: `package-setup-exe.ps1` already uses `IconSourceFile` pointing at `Assets\AppIcon.ico`.
- New dependencies: none.

## Conflict Check

| Check | Result |
| --- | --- |
| Technology stack | Compatible with SDK-style .NET project and WiX 5 packaging. |
| Directory structure | Compatible; icon remains under `src/AliasCockpit.App/Assets/`. |
| Build flow | Compatible; `dotnet publish` embeds the EXE icon and `verify-release.ps1` rebuilds MSI/setup EXE. |
| Offline/network boundary | No runtime network change. |
| License | No new third-party asset or dependency. |
| User requirement | Directly fixes the missing installed icon. |

## Verification Plan

- Run `dotnet build` / `dotnet test` / `dotnet format`.
- Run full `scripts\verify-release.ps1`.
- Extract the associated icon from the published `AliasCockpit.App.exe` and save a PNG for visual inspection.
- Keep MSI validation and setup EXE extraction checks in the release gate.
