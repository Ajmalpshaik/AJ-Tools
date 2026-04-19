# Contributing

AJ Tools should stay easy to maintain. Use a simple branch flow and keep release work disciplined.

## Recommended Flow

1. Branch from the current default branch (`master` today).
2. Use short-lived branches named `feature/<name>`, `fix/<name>`, `docs/<name>`, or `hotfix/<name>` for urgent post-release fixes.
3. Open a pull request, merge after review, then tag the merged default branch for releases.
4. Use `release/<version>` only when a release needs isolated stabilization.

## Before Opening a Pull Request

- Build the project successfully in `Release`.
- Verify the add-in loads in Revit 2020 if behavior changed.
- Update `CHANGELOG.md`, `README.md`, or install docs when user-facing behavior changes.
- Keep unrelated refactors separate from feature work.

## Pull Request Expectations

- State which commands, services, or UI windows changed.
- Note any packaging, installer, or release impact.
- Include screenshots only when the UI changed.
- Keep the scope small enough to review safely.
