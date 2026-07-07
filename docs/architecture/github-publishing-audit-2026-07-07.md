# GitHub Publishing Audit

Date: 2026-07-07

## First-Principles Analysis

- User expectation: initialize this project as a Git repository, create a GitHub repository under `NextWeb4`, push the project, and publish the built Windows release assets.
- Repository name selected from the project: `alias-cockpit`.
- Intended remote: `https://github.com/NextWeb4/alias-cockpit.git`.
- Required invariant: source control must include source, tests, scripts, docs, hard-coded creator information, and release/build instructions; generated build caches, local tools, SQLite/user data, and release binaries should not be committed.
- Release assets belong in GitHub Releases, not in source history. Current `.gitignore` excludes `artifacts/`, so the MSI/setup EXE/portable zip are uploaded as Release attachments.
- Creator identity must remain product-owned constants, not external configuration:
  - `HaoXiang Hwang`
  - `https://nextweb4.github.io/`
  - `didadida1688@gmail.com`

## Scope

- Directly reused: local Git, existing `.gitignore`, existing `scripts\verify-release.ps1`, and existing release artifacts in `artifacts/`.
- Added: `scripts\publish-github-release.ps1` to create/use the GitHub repo, push `main`, create/update `v1.0.0`, and upload release assets once a GitHub token is available from `GITHUB_TOKEN`, `GH_TOKEN`, Git Credential Manager, or the Codex GitHub integration token helper.
- New repository target: `NextWeb4/alias-cockpit`.
- Release target: `v1.0.0`.
- Release assets:
  - `AliasCockpit-win-x64-setup.exe`
  - `AliasCockpit-win-x64.msi`
  - `AliasCockpit-win-x64-portable.zip`

## Conflict Check

| Check | Result |
| --- | --- |
| Current technology stack | Compatible; no project dependency changes. |
| Directory structure | Compatible; Git metadata only adds `.git/`, release notes go under `docs/release/`. |
| Build flow | Compatible; release assets are produced by `scripts\verify-release.ps1`. |
| Offline/runtime boundary | No runtime behavior change. |
| License | No new dependency or external code. |
| Security | `.tools/`, `artifacts/`, build output, SQLite, logs, and encrypted/local files remain ignored. |
| Creator info | Already hard-coded in `ProductCreatorInfo` and locked by `ProductCreatorInfoTests`. |

## Verification Plan

- Run `git status --short` before commit to confirm ignored generated files are not staged.
- Run `scripts\publish-github-release.ps1` to push `main`, create GitHub Release `v1.0.0`, and upload the three release assets.
- Verify remote repository, latest release, and release asset list through GitHub API.

## Token Handling

GitHub CLI is not installed on this machine, so GitHub operations use the project GitHub skill, REST API, and Git HTTPS push. The publish script reads a token only at runtime from an environment variable, Git Credential Manager, or the Codex GitHub integration token helper. It must not write tokens to files, Git remotes, release notes, logs, or committed configuration.
