# Creator Information Audit

Date: 2026-07-05

## Requirement

- Hard-code creator information:
  - Name: `HaoXiang Hwang`
  - Website: `https://nextweb4.github.io/`
  - Email: `didadida1688@gmail.com`
- Creator information must be visible in the app and present in installer metadata.
- Website and email must be clickable in the app; email uses `mailto:`.
- Do not read creator information from environment variables, local config, SQLite, or network.

## Current Project Fit

- Core already contains UI-free constants and business types.
- App can reference Core and render the creator information.
- MSI packaging script already owns Windows Installer metadata.
- Setup EXE packaging script wraps the MSI and can reuse the same manufacturer and website values.

## Option Audit

| Option | Source | License | Core ability | Pros | Cons | Maintenance | Fit | Conflict points | Adopt | Use |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Core hard-coded constants | Current project | Project-owned | Single source of creator truth | Testable, offline, no config drift | Requires rebuild to change | Simple | High | None | Yes | `ProductCreatorInfo` |
| App-only literals | Current project | Project-owned | UI display only | Fast | Installer metadata and tests can drift | Weak | Low | Duplicates values | No | Rejected |
| Config file / environment | Local config | N/A | Runtime override | Flexible | Violates hard-code requirement | Medium | Low | Can be changed outside build | No | Rejected |
| Remote profile fetch | Website/API | Unknown | Dynamic creator profile | Always current | Adds runtime network behavior and failure mode | Unknown | None | Violates offline boundary | No | Rejected |

## Adopted Design

- Directly reused: Core static constants and existing App/Core reference.
- New module: `src/AliasCockpit.Core/Product/ProductCreatorInfo.cs`.
- UI display: result header and window title use `ProductCreatorInfo`; website and email render as `HyperlinkButton` controls.
- Installer metadata: MSI/Setup scripts default `Manufacturer` to `HaoXiang Hwang`, MSI writes contact and website ARP properties.
- Test: `ProductCreatorInfoTests` locks exact hard-coded values.

## Conflict Check

| Check | Result |
| --- | --- |
| Existing tech stack | Compatible; no dependency added. |
| Directory structure | Compatible; product constants live in Core. |
| Runtime behavior | Compatible; no new runtime network call. |
| Database/config | Compatible; no SQLite or config read. |
| Installer packaging | Compatible; metadata values are passed by packaging scripts. |
| User request | Satisfies hard-coded creator information requirement. |
