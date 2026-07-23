# Claude session memory

Running log of decisions and progress across Claude Code chats. Newest entries at
the top. Keep entries short; delete sections that are no longer relevant.

## 2026-07-23 — Smart MEP Tag multi-version compatibility audit (2020–2027)

- Third tool of the passes, same branch/PR (#22). Result: **fully compatible
  2020–2027, zero source changes** — TagCompat's REVIT2023 switch point is exactly
  right (verified: old single-reference IndependentTag members exist 2020–2022 and
  are removed in 2023; multi-reference API added 2022). The TagMode-based
  IndependentTag.Create overload the tool uses exists in all 8 versions.
- LeaderLogicService's reflection fallbacks are runtime-only — compile-safe on all
  versions. All #if blocks in the services are #if DEBUG, not version branches.
- Audit section added to `docs/COMPAT-AUDITS.md`.

## 2026-07-23 — Colorize multi-version compatibility audit (2020–2027)

- Second tool of the one-tool-at-a-time passes, same session/branch/PR (#22) as the
  Filter Pro audit below. Result: **fully compatible 2020–2027, zero source changes**
  (not even header fixes — Colorize's headers are newer and contain no stale claims).
- Colorize-specific members (view-scoped FilteredElementCollector ctor, ToElementIds,
  View.SetElementOverrides, Transaction.RollBack, UIApplication.MainWindowHandle,
  OperationCanceledException) verified identical in all 8 per-version assemblies;
  everything else reuses the surface already verified for Filter Pro.
- Audit section added to `docs/COMPAT-AUDITS.md` (newest first).

## 2026-07-23 — Filter Pro multi-version compatibility audit (2020–2027)

- Ran the per-tool compatibility audit process on **Filter Pro** (first tool of the
  one-tool-at-a-time passes). Result: **fully compatible 2020–2027, zero code changes
  needed** — FilterRuleCompat / ElementIdHelper / ElementIdExtensions already cover
  every real API boundary the tool crosses.
- Verification was done against the real per-version RevitAPI/RevitAPIUI reference
  assemblies (Nice3point NuGet, all 8 versions), decoding method signatures from
  assembly metadata — not just docs. Full record: `docs/COMPAT-AUDITS.md` (new file,
  designed to accumulate one section per audited tool).
- Corrected finding vs older notes: `ElementId(int)` ctor and `ElementId.IntegerValue`
  were both already removed in the **2026** assemblies (not 2027). Helpers switch at
  2024, so nothing breaks.
- Only source changes: stale compatibility notes fixed in the headers of
  FilterCreator.cs / FilterApplier.cs / FilterProDataProvider.cs (documentation only).
- Improvement noted, NOT applied: `View.GetOrderedFilters()` (2021+) could replace
  FilterReorderer's order cache, but would make 2020 behave differently — skipped.
- Cloud sandbox cannot build (no .NET SDK, download blocked by network policy);
  real 8-configuration build still needs to run on the local machine.
- Next tools in the queue when Ajmal asks: pick any ribbon tool and repeat the same
  audit process (script approach documented in COMPAT-AUDITS.md header).

## 2026-07-23 — Claude Code plugin setup

- Added `.claude/settings.json` so every new Claude Code session on this repo
  auto-installs three plugins: `code-review@claude-plugins-official`,
  `superpowers@claude-plugins-official`, `claude-mem@thedotmack` (PR #20, merged).
- claude-mem's cloud sync (paid cmem.ai Pro service) was considered and
  **intentionally skipped**. Cross-chat continuity is handled by this memory file
  plus CLAUDE.md instead. claude-mem still works within a single chat.
- Reminder for future sessions: each cloud chat starts on a fresh machine —
  anything worth keeping must be committed and pushed.

## Open next steps

- None recorded yet.
