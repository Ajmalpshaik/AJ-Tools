# Contributing

AJ Tools should stay easy to maintain. Use a simple workflow and keep release work disciplined.

## Branching

- Start from the current default branch.
- Use short-lived branches such as `feature/<name>`, `fix/<name>`, or `docs/<name>`.
- Do not keep a permanent `dev` branch.
- Use a `release/<version>` branch only when a release needs isolated stabilization. Otherwise release from the default branch.

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
