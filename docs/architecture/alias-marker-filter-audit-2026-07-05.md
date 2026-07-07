# Alias Marker Filter Audit

Date: 2026-07-05

## Requirement

- Distinguish aliases that have site/purpose/color markers from aliases without markers.
- Keep existing marker persistence and avoid database schema churn.
- Improve the result view without introducing runtime network behavior or new product dependencies.

## First-Principles Analysis

- Input: generated alias addresses plus optional persisted metadata keyed by address.
- State: an alias is marked when at least one of `Site`, `Purpose`, or `Color != None` is present.
- Output: the user needs to see and filter marked and unmarked aliases.
- Broken UX invariant before this change: rows with and without markers were not directly filterable, and the distinction could depend too much on muted summary text or color.
- Minimum fix: compute marked/unmarked state in `MainPageViewModel`, add `marked` / `unmarked` filters, and show an explicit Marked/Unmarked badge in each row.

## Option Audit

| Option | Source | License | Core ability | Pros | Cons | Maintenance | Fit | Conflict points | Adopt | Use |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ViewModel filter over existing metadata | Current project | Project-owned | Count and filter generated aliases by marker state | No schema change, testable, fast for current generated result size | Counts are scoped to current generated result set | Simple | High | None | Yes | `marked` / `unmarked` filters |
| Database-level marked query | Current SQLite repository | Project-owned | Query all marked aliases | Useful for a future alias manager | Overkill for generated current result view | Medium | Low for this request | Would change repository surface | No | Deferred |
| New UI state library | NuGet | Varies | Badges/filter chips | Could polish UI | New dependency for simple controls | Extra burden | Low | Violates minimal dependency preference | No | Rejected |

## Adopted Design

- Directly reused: existing `AliasRecord` metadata and `GeneratedAliasRowViewModel.IsMarked`.
- New UI behavior: filter buttons `Marked N` and `Unmarked N`.
- New visual behavior: row badge shows `Marked` or `Unmarked`; color is supplemental, not the only signal.
- New test: `AliasCockpit.App.Tests` verifies marked/unmarked counts and filters.

## Conflict Check

| Check | Result |
| --- | --- |
| Existing tech stack | Compatible; WinUI controls only. |
| Directory structure | Compatible; ViewModel remains in App, tests in `tests/AliasCockpit.App.Tests`. |
| Database schema | No change. |
| Runtime network behavior | No change. |
| Accessibility | Improved because marker state is text and AutomationProperties include it. |
| User request | Satisfies marked vs unmarked distinction. |
