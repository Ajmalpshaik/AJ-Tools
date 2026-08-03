---
name: ajtools-build
description: Build a new tool, command, feature, ribbon button, or automation for the AJ Tools Revit C# add-in (D:\Ajmal\Revit Addins). Use this whenever Ajmal describes a new piece of work he wants added to AJ Tools specifically — a new command, report, export, or automation — even in plain modeller language and even if he never says "build" ("I need something that renumbers...", "make a tool for...", "add a button that..."). This skill remembers AJ Tools' house conventions and past decisions between sessions via a shared local log file, so new work automatically follows established patterns instead of reinventing them from scratch each time. Do NOT use this for fixing or checking something that already exists and is broken — use ajtools-debug for that instead. Do NOT use this for Revit C# work outside the AJ Tools project — use the general revit-csharp-plugin skill for that.
---

# AJ Tools — Build New Work

Ajmal is a BIM/Revit modeller, not a developer. He describes what he wants in plain, sometimes broken
English (often dictated). Your job is to turn that into a clean, production-ready piece of the AJ Tools
add-in — and to leave the project's shared memory a little smarter than you found it, so the next build
or debug session doesn't have to re-learn the same conventions.

## How to work: plan, split, then execute

Don't jump straight from Ajmal's request to a single opaque action. Break it into a short visible plan
first — this is how a good team works: one clear step finishes cleanly before the next one starts,
instead of everything happening in one tangled step where a mistake early on quietly wrecks everything
after it.

For example, if Ajmal asks for a new tool that "finds every VCD and reports the total duct length
connected to each one": the plan is Step 1 — confirm what "VCD" and "connected duct length" mean
precisely (VCD is a family within Duct Accessories; "connected" means ducts joined at its connectors).
Step 2 — check the codebase for existing services that already do part of this (element collection
helpers, connector-walking logic) so nothing gets reinvented. Step 3 — write the command, following house
conventions. Step 4 — compile-check it (and live-test via AJ AI Bridge if connected). Step 5 — update the
knowledge log. Each step's output feeds the next, and if step 1 reveals a wrong assumption about what
"connected" means, you find out before writing a single line of the wrong logic.

The same discipline applies to any shape of build, not just this one — e.g. "add a tool that draws a wall
along this path" still splits into: confirm inputs (path/level/wall type) → write the creation logic →
verify it compiles and behaves as expected → update the log. Use `TaskCreate`/`TodoWrite` to track the
steps so progress is visible, and check each step's result before moving to the next rather than firing
off every step and hoping.

Note: a plain question like "how many VCDs are there right now" is **not** a build task at all — it's a
one-off query about the live model, answered directly with the Revit API, no new capability involved.
Don't route ordinary questions through this skill; it's specifically for adding something new to AJ Tools.

**When to actually split work across separate agents, not just separate steps:** if the task is large
enough that pieces of it are genuinely independent — the same operation repeated across many levels or
views, several unrelated pieces of a bigger build, anything where one part's outcome doesn't depend on
another's — use the `Agent` tool to hand pieces off like a relay team, one finishing before (or alongside)
the next picks up. But for a normal-sized task, do the steps yourself, directly, with visible tracking —
spinning up a separate agent for something small (a single count, a single wall) adds overhead for no
real benefit. Match the amount of ceremony to the actual size of the job.

## Before you write any code

1. **Read the knowledge files**: [`ajtools-conventions.md`](../../knowledge/ajtools-conventions.md) (house
   conventions — branding, metadata block format, version-safe API helpers, tag/leader logic rules,
   current build reality, plus a running log of past decisions) and
   [`glossary.md`](../../knowledge/glossary.md) (Ajmal's spoken terms → exact Revit/AJ Tools meaning —
   check this before guessing what an ambiguous or garbled term means, e.g. "fitting" isn't always Duct
   Fitting). If either file is missing, recreate it from what this skill already knows and note that it
   was missing.
2. **Understand the ask.** If Ajmal's request is ambiguous in a way that would change the actual
   implementation (which elements/categories, what triggers it, what the output looks like), ask —
   briefly, with a sensible recommended default, the way you would for any other Revit automation task.
   Don't ask about things the conventions log already answers.
3. **Check whether this overlaps existing code.** Search the codebase (`src/Commands`, `src/Services`,
   `src/Models`) for anything similar before writing new files — AJ Tools has a lot of existing services
   (LeaderLogicService, ElementIdHelper, RevitCompat, TagCompat, ValidationHelper, DialogHelper, etc.).
   Reuse them; don't reimplement what already exists.

## While building

Follow the conventions in the knowledge log exactly — they exist because Ajmal hit real bugs or made
deliberate calls, not because someone likes rules for their own sake. In particular:

- Full `#region Metadata` header on every new command/service file.
- Route all version-sensitive Revit API calls through the existing compatibility helpers
  (`ElementIdHelper`, `RevitCompat`, `TagCompat`, `FilterRuleCompat`) rather than calling the raw API —
  this project targets Revit 2020 through 2027 simultaneously, and several raw API members silently
  compile but crash or misbehave on newer versions.
- Any new tag-placement or leader logic goes through `LeaderLogicService` — don't write a one-off elbow
  calculation.
- Register new ribbon buttons in `Core/RibbonManager.cs` or `Core/AnnotationRibbonManager.cs`, matching
  the existing pattern for that panel.
- If you're genuinely unsure whether a Revit API member exists/behaves the same across 2020-2027, don't
  guess from memory or general web knowledge — this codebase has already been burned by that. If AJ Tools
  is currently connected via the AJ AI Bridge (`mcp__aj-tools-aj-ai__ping`), you can
  verify live against whichever Revit version Ajmal has open.

## After you finish

Update the knowledge log — this is the step that makes the skill better day by day, so don't skip it.
Append a short dated entry under **Log** in
[`.claude/knowledge/ajtools-conventions.md`](../../knowledge/ajtools-conventions.md) describing:

- What was built (one line).
- Any *new* convention or decision that came up during this task — a naming choice Ajmal confirmed, a
  pattern you had to invent because nothing existing covered it, a Revit-version quirk you discovered.
  Skip this if nothing new came up; don't pad the log with restatements of what's already there.

If the "Established Conventions" section itself turns out to be wrong or stale while you're working
(e.g. a file moved, a helper got renamed), fix it in place rather than leaving the log to drift from
reality.

Also consider, per the existing versioning convention: does this change warrant a suite version bump in
`Properties/AssemblyInfo.cs`? A brand-new tool = bump minor. A fix within an existing tool with no new
capability = bump patch, and that's more `ajtools-debug`'s territory anyway.
