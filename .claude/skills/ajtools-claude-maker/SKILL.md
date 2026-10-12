---
name: ajtools-claude-maker
description: Create a NEW skill in this repo's .claude/skills when a recurring, bounded, multi-step AJ Tools task pattern is spotted — either because Ajmal asks ("make this a skill", "save this workflow") or proactively mid-work when the same shape of request keeps coming back. Create it, then report it in the same reply ("created skill X because Y — say delete if you don't want it") — the report is mandatory, silent creation is forbidden. Do NOT use for one-off tasks, for universal habits that must apply to every task regardless of skill (those belong in CLAUDE.md or a knowledge file), or for modelling work inside a Revit model (that belongs to the separate Heron AI project, never touched from here). Deleting or replacing an EXISTING skill always needs Ajmal's explicit OK first.
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
   ones the session also offers (github-aj-tools, revit-plugin-csharp, revit-tool-pyrevit,
   revit-ui-design, ...). If an existing skill
   already covers the pattern, improve that one instead of creating a rival — two skills claiming the
   same request poisons routing for every future session.
3. **Code goes elsewhere.** Reusable C# belongs in the plugin source (`src/`), with the skill merely
   pointing at it — never paste working C# into a SKILL.md.

## Building the skill

1. Create `.claude/skills/<kebab-name>/SKILL.md` — one skill per folder, like the existing ones.
2. Frontmatter: `name` + a deliberately pushy `description` — what it does AND the situations that
   trigger it, including broken-English/dictated phrasings Ajmal actually uses, AND what it must NOT
   fire on, naming the skill that owns that instead.
3. Bake in plan-split-execute: a short visible plan, one step at a time, each verified before the next —
   never one opaque action.
4. Point at shared knowledge (`ajtools-conventions.md`, `debug-log.md`) — don't
   duplicate facts into the skill.
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
- Never create a skill for modelling work inside a Revit model — AJ Tools has no live link to Revit;
  that work belongs to the separate Heron AI project.
