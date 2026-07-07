# Email Alias Expander Count Audit

Date: 2026-07-05

## First-Principles Analysis

- User expectation: when Gmail dot aliases count is 32 and `+tag` aliases count is 32, the `All` filter must show 64 unique generated addresses.
- Actual behavior before fix: `All` showed 32 because the merged list was capped again by `Count`.
- Input: valid Gmail/Googlemail address, enabled dot aliases, enabled plus aliases, `Count = 32`.
- State transition: Core generated up to `Count` dot aliases and up to `Count` plus aliases, then interleaved both lists.
- Broken invariant: `Aliases` should be the de-duplicated union of generated categories, not a second per-category capped list.
- Faulty layer: `AliasCockpit.Core.Tools.EmailAliasExpander` applied `.Take(count)` to the final merged list.
- Minimum fix: keep per-category caps on `DotAliases` and `PlusAliases`, remove the final cap on `Aliases`.

## Regression Test

- Added `AllAliasesAreUnionOfDotAndPlusAliasesWithoutPerCategoryCap`.
- The test asserts `DotAliases.Count == 32`, `PlusAliases.Count == 32`, and `Aliases.Count == 64`.
- Existing normalization test now asserts `Aliases.Count` equals the case-insensitive distinct union of dot and plus aliases.
- Added `MainPageViewModelTests.AllFilterCountMatchesDotAndPlusUnion` so the App/ViewModel `All` filter and summary also stay at 64 when dot and plus counts are both 32.

## Risk Check

| Risk | Result |
| --- | --- |
| Duplicate output | Controlled by existing case-insensitive `Distinct`. |
| UI count mismatch | Fixed because UI reads `_result.Aliases.Count`. |
| Per-category limit regression | Not changed; each category still uses `Count`. |
| Large output growth | Maximum merged Gmail output is bounded by current two categories, up to 512 addresses. |
| Runtime network behavior | No change. |
| New dependencies | None. |

## Adopted Scope

- Directly reused: existing Core generation code and distinct/interleave helpers.
- Borrowed design: none.
- Not adopted: new alias generation libraries; the bug is a local invariant violation and does not need a dependency.
- Existing modules retained: App ViewModel/UI count bindings remain unchanged.
