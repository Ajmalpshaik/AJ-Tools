---
name: ajtools-panel-audit
description: Run a behaviour-preserving audit/refactor pass over one AJ Tools ribbon panel, one tool, or the whole project — bring every file up to house standards (metadata blocks, version-safe API helpers, transaction/undo hygiene, guards, popup rules, ribbon wiring, orphaned resources) WITHOUT changing what any tool actually does. Use whenever Ajmal says things like "audit the datums panel", "clean up the graphics tools", "refactor this panel", "check all the tools in the MEP panel", "full project audit", "make it production ready", "standardize the annotation tab", or broken-English/dictated versions ("chek the panel", "clean the tools", "make it professnal"). This is the workflow that produced suite versions 1.5.2 → 1.8.0 — one panel at a time, behaviour unchanged, version bumped, changelog updated. Do NOT use this for fixing a specific reported bug — that's ajtools-debug. Do NOT use this for adding new capability — that's ajtools-build. Do NOT use this for GitHub/release work — that's the aj-tools-github skill.
---

# AJ Tools — Panel Audit / Refactor Pass

This is the skill for Ajmal's most repeated plugin-side job: taking one ribbon panel (or one tool, or the
whole project) and bringing every file in it up to house standards without changing any tool's behaviour.
It happened by hand at least six times (Datums, Graphics, View Crop, Annotation tab, Modify/MEP/
Coordination/Data/Manage/Family, then a full-project pass) before becoming this skill — the steps below
are exactly what those passes did.

**The golden rule: behaviour-preserving.** An audit pass cleans up *how* the code is written, never *what*
the tool does. If a genuine behaviour improvement suggests itself mid-pass (a missing confirmation, a
smarter default), flag it to Ajmal as a separate item and let him decide — don't fold it in silently. The
one standing exception, established across the past passes: **removing success popups is allowed** (silent
success is the house style), and **adding a confirmation dialog before bulk edits** is allowed — both were
Ajmal-approved as standard parts of these passes.

## How to work: plan, split, then execute

Confirm the scope first (which panel / tool / whole project), then run the pass one tool at a time — not
all files in one opaque sweep. For each tool, finish its checklist, verify it still compiles, then move to
the next. Use task tracking so Ajmal can see which tool the pass is on.

### Step 1 — Read the knowledge files

[`ajtools-conventions.md`](../../knowledge/ajtools-conventions.md) (the standards this pass enforces),
[`debug-log.md`](../../knowledge/debug-log.md) (known bug history — don't reintroduce one), and
[`glossary.md`](../../knowledge/glossary.md) if any term in the request is ambiguous.

### Step 2 — Inventory the scope

Map the panel's buttons to files: read `Core/RibbonManager.cs` / `Core/AnnotationRibbonManager.cs` for the
panel's wiring, list every command class, service, UI file, and icon it references. This inventory is the
checklist backbone — nothing in scope gets skipped, nothing out of scope gets touched.

### Step 3 — Per-tool checklist (the house standards)

For every file in scope, check and fix:

1. **Metadata**: full `#region Metadata` block (template: `Properties/AssemblyInfo.cs`,
   `Helpers/ElementIdHelper.cs`), with the tool's own version/changelog updated for this pass.
2. **Version-safe API**: raw calls routed through the compat helpers — `ElementIdHelper` (including
   `FromInt`, `IsDefinedBuiltInCategory/Parameter`), `RevitCompat`, `TagCompat`, `FilterRuleCompat`;
   dimension collectors by category, not `OfClass(typeof(Dimension))`. **Scope-hold rule**: fix the files
   in this pass's scope only — do NOT expand into the known project-wide `ElementIdHelper` migration
   without Ajmal's explicit sign-off.
3. **Transactions/undo**: multi-step user actions grouped so one Ctrl+Z undoes the whole thing
   (`TransactionGroup` + assimilate), transaction names `"AJ Tools - <Tool>"`.
4. **Guards**: Family-Editor-only tools guarded with an availability class *assigned in the ribbon
   wiring* (a defined-but-unwired availability class was a real finding once); no-document paths cancel
   cleanly.
5. **Popup rules**: no success popups (silent success); confirmation dialog before bulk edits stating
   what will happen and how many elements are affected.
6. **Tag/leader logic**: anything computing leader elbows goes through `LeaderLogicService` — never a
   local copy of that logic.
7. **Shared helpers**: validation via `ValidationHelper`, dialogs via `DialogHelper` — no re-implemented
   one-offs.
8. **Ribbon consistency**: button label/tooltip spelling consistent (an "Aj tool" label was a real
   finding), icon files actually exist, no orphaned icons/resources left referenced by nothing.

### Step 4 — Verify

Build clean with deploy skipped —
`msbuild "AJ Tools.sln" -p:Configuration=Release -p:Platform=x64 -p:SkipAjToolsAutoDeploy=true` —
zero errors, zero warnings. If the pass touched version-sensitive API, build at least one newer config too
(e.g. `Release R25`). If the AJ AI Bridge is connected and a tool's effect is observable in the
model, spot-check one live; otherwise state plainly that Revit wasn't launched.

### Step 5 — Version + changelog

Bump the suite version in `src/Properties/AssemblyInfo.cs` (patch — refactor only, no new tool), add the
entry to its metadata changelog AND `CHANGELOG.md`, ending with the standard honesty line about what was
and wasn't tested ("All tool behaviour unchanged", "not loaded in Revit" if true).

### Step 6 — Report + capture

Report per the pattern of past passes: what was confirmed clean, what was fixed, what was removed, known
accepted debt left alone. Append one dated line to `ajtools-conventions-log.md`; a new convention
discovered mid-pass goes into its Established Conventions section instead (one fact, one file).
