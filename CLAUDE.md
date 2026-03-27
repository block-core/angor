# Angor Project Rules

## Branching & PR Workflow

- **Never commit directly to `main`.** Always create a new feature branch before starting any code changes.
- Branch naming: `feat/short-description`, `fix/short-description`, or `refactor/short-description`.
- When work is ready, push the branch and create a PR to `main` for review.
- Do not merge PRs — they require review first.

## Scope

- Only modify files under `src/Angor/Avalonia/Avalonia2/` (the UI layer).
- Do not touch `AngorApp/`, `AngorApp.Model/`, `Angor.Sdk/`, or other projects outside Avalonia2.
- Exception: CI/infra files (`.github/workflows/`, `.gitea/workflows/`) and new sibling host projects (e.g., `Avalonia2.Android/`) that reference Avalonia2.
- Existing SDK connection points may be used but not modified.
