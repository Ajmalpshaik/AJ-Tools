---
name: ajtools-claude-maker
description: Create a NEW skill in this repo's .claude/skills when a recurring, bounded, multi-step AJ Tools task pattern is spotted — either because Ajmal asks ("make this a skill", "save this workflow") or proactively mid-work when the same shape of request keeps coming back. Create it, then report it in the same reply ("created skill X because Y — say delete if you don't want it") — the report is mandatory, silent creation is forbidden. Do NOT use for one-off tasks, for universal habits that must apply to every task regardless of skill (those belong in CLAUDE.md or a knowledge file), for reusable C# fragments (those go in .claude/scripts per its README), or for the AJ AI Brain at D:\Ajmal\AJ AI Brain (the Brain maintains itself via its own brain-self-maintain skill). Deleting or replacing an EXISTING skill always needs Ajmal's explicit OK first.
---

# AJ Tools Skill Maker

Turns a recurring task pattern into a new skill folder under `.claude/skills/`, following the same
create-then-report rule Ajmal set on 2026-07-16.

## Before creating — three gates

1. **Is it really skill-shaped?** Recurring + bounded + multi-step. A standing preference ("always
   answer counts in one line") is NOT a skill — a skill only fires when routing decides it applies, but a
   universal habit must apply always, so it belongs in CLAUDE.md or the matching knowledge file instead.
   Getting this wrong produces a skill that never triggers.
2. **Overlap check.** Read the descriptions of every existing skill in `.claude/skills/` and the
   plugin-provided ones visible in the session (ajtools-build, ajtools-debug, ajtools-panel-audit,
   ajtools-port-pyrevit, aj-tools-github, revit-csharp-plugin, revit-ui, ...). If an existing skill
   already covers the pattern, improve that one instead of creating a rival — two skills claiming the
   same request poisons routing for every future session.
3. **Code goes elsewhere.** If the reusable part is C# for the live model, it belongs in
   `.claude/scripts/` (route via that folder's README) with the skill merely pointing at it — never
   paste working C# into a SKILL.md.

## Building the skill

1. Create `.claude/skills/<kebab-name>/SKILL.md` — one skill per folder, matching the existing four.
2. Frontmatter: `name` + a deliberately pushy `description` — what it does AND the situations that
   trigger it, including broken-English/dictated phrasings Ajmal actually uses, AND what it must NOT
   fire on, naming the skill that owns that instead.
3. Bake in plan-split-execute: a short visible plan, one step at a time, each verified before the next —
   never one opaque action.
4. Point at shared knowledge (`glossary.md`, `reply-style.md`, `ajtools-conventions.md`,
   `knowledge/live-model/` via its README) — don't duplicate facts into the skill.
5. Size rule: a SKILL.md stays roughly 60–150 lines — when to trigger and the steps, not reference
   material.
6. Run `.claude/tools/verify-knowledge-consistency.ps1` — the new skill's frontmatter and any links it
   adds must pass.

## After creating — the mandatory report

In the same reply, in plain language: **"Created skill X because Y — say delete if you don't want
it."** Include what it fires on and what it deliberately leaves to other skills. Note that a newly
created skill file is picked up when a session (re)starts — it may not be invocable in the very session
that created it.

## Hard limits

- Never delete or replace an existing skill without Ajmal's explicit OK — create-then-report covers
  NEW skills only.
- Never create a skill for Brain-side modeling workflows — since the 2026-07-22 split, day-to-day
  modeling skills live in `D:\Ajmal\AJ AI Brain\skills\`, and that folder maintains itself.
