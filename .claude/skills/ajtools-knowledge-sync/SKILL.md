---
name: ajtools-knowledge-sync
description: On-demand, deliberate full-session sweep of this repo's .claude — use when Ajmal asks "did we save everything", "sync the knowledge", "did we miss anything", "update your notes", or when a long working session is wrapping up and it's worth checking nothing valuable got left behind in chat. Re-reads the whole session, routes every new fact/technique/correction/script into exactly one right home, then runs the consistency checker. Do NOT use for the lightweight per-task capture that CLAUDE.md discipline #2 already requires after every task (that's a standing habit, not this skill), for the AJ AI Brain at D:\Ajmal\AJ AI Brain (its own brain-self-maintain skill owns that), or as an excuse to restructure/split files that aren't demonstrably too big (measure line counts first — Ajmal's split-and-index rule).
---

# AJ Tools Knowledge Sync

The deliberate, deeper version of the always-on capture habit: one pass over the whole session so
nothing worth keeping stays trapped in chat history.

## The sweep

1. **Re-read the session** and list candidates: new techniques or gotchas, new ambiguous/dictated terms,
   reply-format corrections, bugs found + fixed, reusable C#, coding conventions or decisions, facts
   about Ajmal or ongoing work that should follow him across sessions.
2. **Read the current state of every likely destination before writing** — never duplicate something
   already captured, and never write the same fact into two files.
3. **Route each item to exactly one home** (this is CLAUDE.md discipline #2's table — it wins if the two
   ever differ):
   - AJ Tools coding convention/decision → `knowledge/ajtools-conventions.md`
   - Ambiguous term or dictation quirk → `knowledge/glossary.md`
   - AJ AI Bridge / live-model gotcha → the matching topic file in `knowledge/live-model/` (route via
     its README index)
   - Reply-format correction → `knowledge/reply-style.md`
   - Bug found + fixed → `knowledge/debug-log.md`
   - Reusable live-model C# → `.claude/scripts/` per that folder's README (compose filters + actions;
     bespoke multi-stage builds go in `recipes/`), updating the README table in the same step
   - Cross-session fact about Ajmal / standing feedback / project state → the memory system at
     `C:\Users\AjmalAlavudheen\.claude\projects\D--Ajmal-Revit-Addins\memory\` (one file per fact, then
     add its one-line pointer in `MEMORY.md` — an unindexed memory is never recalled)
4. **Recurring task pattern spotted?** Don't scaffold it inline — hand it to `ajtools-claude-maker`
   (create-then-report rule applies).
5. **Run the checker**: `.claude/tools/verify-knowledge-consistency.ps1` — fix anything it flags before
   reporting.
6. **Report back** a short plain-language list: what got added or updated, and where. Ajmal should not
   have to open the files to know what changed.

## Rules that keep the sweep honest

- One fact, one home — duplication is what makes the indexes untrustworthy.
- A knowledge file past ~300 lines is a split *candidate*, not a mandate — measure first, and if it
  reads as one coherent job, say so and leave it alone.
- When anything is added, split, or retired, update its index in the same step.
- Facts recorded here describe this repo (plugin dev). Since the 2026-07-22 split, day-to-day modeling
  knowledge belongs to the Brain — if a modeling-only fact surfaces here, note it for Ajmal rather than
  silently writing it into the wrong tree.
