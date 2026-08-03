---
name: ajtools-debug
description: Check, test, or debug an EXISTING tool/command in the AJ Tools Revit C# add-in (D:\Ajmal\Revit Addins) — use whenever Ajmal reports something broken, wrong, crashing, giving bad results, or asks to "check"/"test"/"debug" a tool he already has, even without those exact words ("why does X give wrong sizes", "my duct tag button isn't working right", "check the ceiling magnet tool", "this tool gave me the wrong count"). When Revit is open and the AJ AI Bridge is connected, this skill verifies the bug AND the fix live against the real model instead of just reading code — always check with mcp__aj-tools-aj-ai__ping first. Remembers past bugs, root causes, and conventions between sessions via a shared local log file. Do NOT use this for building brand-new capability from scratch — use ajtools-build for that instead.
---

# AJ Tools — Debug an Existing Tool

Ajmal is a BIM/Revit modeller, not a developer — he'll describe a symptom ("it gives the wrong size",
"nothing happens when I click it"), not a stack trace. Your job is to find the actual tool in the
codebase, work out what's really going wrong, fix it, and — where possible — prove the fix works against
his real live model rather than just "looks right in the code."

## How to work: plan, split, then execute

The 5 steps below are the general shape, but for each actual debugging task, work out the specific
version of that plan for this bug before diving in — e.g. "the ceiling magnet gives the wrong grid size"
becomes: Step 1 — locate `CmdCeilingMagnet.cs` and its grid-detection logic. Step 2 — check the real
ceiling family's parameters live via AJ AI Bridge. Step 3 — compare what the code assumes against what's
actually there. Step 4 — fix the mismatch. Step 5 — re-verify live. Treat this like a relay: finish one
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
- [`.claude/knowledge/glossary.md`](../../knowledge/glossary.md) — Ajmal dictates a lot of requests and
  terms get garbled or are genuinely ambiguous (e.g. "fitting" could mean Duct Fitting *or* Pipe Fitting —
  don't assume, check context or ask). Check this before guessing what a term means.

## Step 2 — Find the tool

Search `src/Commands`, `src/Services`, `src/Models` for the command Ajmal is describing. AJ Tools names
things descriptively (`CmdSmartMepTag.cs`, `CmdCeilingMagnet.cs`, etc.) but Ajmal won't know the file name
— match on what the tool visibly does (ribbon button text, tooltip, behavior) rather than expecting an
exact name.

## Step 3 — Reproduce it for real when you can

Check the AJ AI Bridge before assuming you can only read code:

```
mcp__aj-tools-aj-ai__ping
```

- **If it responds "pong"** (Revit is open, bridge connected): use `run_csharp` to query the live document
  and actually reproduce the reported symptom against the real model — read the actual parameter values,
  actual element states, actual counts. Check [`.claude/scripts/`](../../scripts/README.md) first —
  composing a `filters/` fragment with `actions/action-count-and-report.cs`, or running a `recipes/`
  script like `verify-duct-connectivity.cs`, may already be the fastest way to pull the real state needed
  to confirm or rule out the bug, instead of writing a throwaway query. This turns "I think this is the
  bug" into "I confirmed this is the bug," which matters
  because plausible-looking C# can still be wrong about what Revit's API actually returns on Ajmal's
  installed version. After fixing the source, re-verify live where the fix is something observable that
  way (a value, a count, a created element) — not everything can be live-checked (e.g. a pure UI/ribbon
  issue), use judgment.
- **If it's not connected**, say so plainly to Ajmal rather than silently skipping this step, then fall
  back to careful static reading of the code and Revit API version behavior. Tell him the fix is
  code-reviewed but not yet live-verified, so he knows to test it himself.
- The bridge refuses reflection/assembly-loading and destructive operations (Delete/Purge/file writes)
  by design — don't try to route around either of those; they're deliberate safety limits, not bugs in
  the bridge.

## Step 4 — Fix it, following the house conventions

Apply the fix in source, keeping it consistent with what's in the knowledge log (version-safe API
helpers, `LeaderLogicService` for any tag/leader logic, existing metadata block format, etc.). A bug fix
doesn't need surrounding refactoring — fix the actual defect, don't restyle the file around it unless
Ajmal asked for that too.

## Step 5 — Update the knowledge files

This is what makes the skill sharper over time — don't skip it.

- Append an entry to [`.claude/knowledge/debug-log.md`](../../knowledge/debug-log.md) using its format:
  symptom (Ajmal's words) → root cause (often different from the first guess — that gap is exactly what's
  worth recording) → fix → verified how (live via AJ AI Bridge, or code-review only) → date.
- If a *new* term or ambiguity came up (like the fitting/pipe-fitting ambiguity), add it to
  [`glossary.md`](../../knowledge/glossary.md) instead.
- If the fix depended on or changed a coding convention, update
  [`ajtools-conventions.md`](../../knowledge/ajtools-conventions.md) instead.

Each fact goes in exactly one file — don't duplicate the same entry across two of them.

If this fix reveals a *pattern* likely to recur elsewhere (e.g. the same raw-API call copy-pasted into
three other tools), say so in the log entry and flag it to Ajmal — don't silently go fix the other three
without asking, since that widens scope beyond what he asked for.
