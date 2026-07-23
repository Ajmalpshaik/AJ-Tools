# CLAUDE.md

## Project

AJ Tools is a C# Revit add-in suite (WPF/XAML UIs). One `src/` codebase builds for
Revit 2020–2027 via per-version build configurations (see `Directory.Build.props`);
Revit 2020 (.NET Framework 4.7.2) is the tested baseline the published installer ships.
Public installers live in the separate AJ-Tools-Installer repository. See README.md
for the full compatibility table.

## Working with Ajmal

Ajmal is a BIM/Revit modeller, not a developer. Explain everything in plain,
non-developer language (Revit terms are fine). Confirm before merging to master,
creating releases, deleting things, or any other action that changes the remote
repository beyond the session's own work branch.

## Session memory (repo-based)

Cloud sessions run on fresh, temporary machines, so this repository is the memory:

1. **At session start:** read `docs/CLAUDE-MEMORY.md` to catch up on decisions and
   progress from previous chats.
2. **Before finishing significant work:** append a short dated entry to
   `docs/CLAUDE-MEMORY.md` — what changed, decisions made, open next steps — and
   commit it together with your other changes so it persists once merged.

Keep entries brief; prune stale ones when a section no longer matters.
