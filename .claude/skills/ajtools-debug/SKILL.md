---
name: ajtools-debug
description: Check, test, or debug an EXISTING tool/command in the AJ Tools Revit C# add-in (D:\06_Development\01_AJ-Tools\Revit Addins) — use whenever Ajmal reports something broken, wrong, crashing, giving bad results, or asks to "check"/"test"/"debug" a tool he already has, even without those exact words ("why does X give wrong sizes", "my duct tag button isn't working right", "check the ceiling magnet tool", "this tool gave me the wrong count"). Remembers past bugs, root causes, and conventions between sessions via a shared local log file. Do NOT use this for building brand-new capability from scratch — use ajtools-build for that instead.
---

# AJ Tools — Debug an Existing Tool

Ajmal is a BIM/Revit modeller, not a developer — he'll describe a symptom ("it gives the wrong size",
"nothing happens when I click it"), not a stack trace. Your job is to find the actual tool in the
codebase, work out what's really going wrong, fix it, and — where possible — prove the fix works against
his real live model rather than just "looks right in the code."

## How to work: plan, split, then execute

The 5 steps below are the general shape, but for each actual debugging task, work out the specific
version of that plan for this bug before diving in — e.g. "the ceiling magnet gives the wrong grid size"
becomes: Step 1 — locate `CmdCeilingMagnet.cs` and its grid-detection logic. Step 2 — ask Ajmal for the real
ceiling family's parameters (or read them from the family). Step 3 — compare what the code assumes against what's
actually there. Step 4 — fix the mismatch. Step 5 — Ajmal re-tests in Revit. Treat this like a relay: finish one
step cleanly, confirm its result, then hand off to the next — don't let an unverified assumption from
step 2 quietly become the foundation for steps 3 and 4.

If Ajmal reports several genuinely unrelated problems in the same message (e.g. two different tools
misbehaving for unrelated reasons), it's fine to use the `Agent` tool to investigate each independently —
like splitting the work across a small team — rather than context-switching between them yourself one
line at a time. For a single bug in a single tool, work it yourself directly with visible steps; spinning
up a separate agent for one bug in one file is more overhead than it's worth.

## Step 1 — Read the knowledge files first

Read three small files before doing anything else — each covers a different concern, don't skip any:

- [`.claude/knowledge/ajtools-conventions.md`](../../knowledge/ajtools-conventions.md) — house coding
  conventions, so your fix doesn't introduce a new violation.
- [`.claude/knowledge/debug-log.md`](../../knowledge/debug-log.md) — past bugs, root causes, and fixes.
  The symptom Ajmal describes today may be a repeat of something already diagnosed — recognizing that
  instantly instead of re-deriving it is the whole point of this skill.
- Ajmal dictates a lot of requests and terms get garbled or are genuinely ambiguous (e.g. "fitting"
  could mean Duct Fitting *or* Pipe Fitting) — don't assume, check context or ask.

## Step 2 — Find the tool

Search `src/Commands`, `src/Services`, `src/Models` for the command Ajmal is describing. AJ Tools names
things descriptively (`CmdSmartMepTag.cs`, `CmdCeilingMagnet.cs`, etc.) but Ajmal won't know the file name
— match on what the tool visibly does (ribbon button text, tooltip, behavior) rather than expecting an
exact name.

## Step 3 — Reproduce it for real when you can

There is no live connection to Revit any more (the AJ AI Bridge was removed in v1.57.0), so this
step is careful reading, not live reproduction:

- Read the code path end to end and check the Revit API behaviour against the real installed
  `RevitAPI.dll` for the versions involved, never from memory.
- Tell Ajmal plainly that the fix is code-reviewed but not live-verified, and give him the exact
  steps to reproduce and re-test it in Revit himself.

## Step 4 — Fix it, following the house conventions

Apply the fix in source, keeping it consistent with what's in the knowledge log (version-safe API
helpers, `LeaderLogicService` for any tag/leader logic, existing metadata block format, etc.). A bug fix
doesn't need surrounding refactoring — fix the actual defect, don't restyle the file around it unless
Ajmal asked for that too.

## Step 5 — Update the knowledge files

This is what makes the skill sharper over time — don't skip it.

- Append an entry to [`.claude/knowledge/debug-log.md`](../../knowledge/debug-log.md) using its format:
  symptom (Ajmal's words) → root cause (often different from the first guess — that gap is exactly what's
  worth recording) → fix → verified how (Ajmal tested in Revit, or code-review only) → date.
- If the fix depended on or changed a coding convention, update
  [`ajtools-conventions.md`](../../knowledge/ajtools-conventions.md) instead.

Each fact goes in exactly one file — don't duplicate the same entry across two of them.

If this fix reveals a *pattern* likely to recur elsewhere (e.g. the same raw-API call copy-pasted into
three other tools), say so in the log entry and flag it to Ajmal — don't silently go fix the other three
without asking, since that widens scope beyond what he asked for.
