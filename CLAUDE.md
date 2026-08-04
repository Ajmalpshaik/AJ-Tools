# AJ Tools — Working Agreement

## What this project is

AJ Tools is Ajmal's own Revit add-in: a C# / WPF plugin that adds two ribbon tabs ("AJ Tools" and
"AJ Annotation") of view, graphics, datum, MEP, coordination, annotation, dimensioning, and tagging
tools, plus a built-in AI shell (AJ AI). Facts a session should not have to rediscover:

- Solution: `AJ Tools.sln` at the repo root. **Live source tree: root `src/`** (`src/AJ Tools.csproj`) —
  Commands, Core (ribbon managers), Helpers, Models, Services, UI, AiShell, Resources.
- One codebase builds **Revit 2020 → 2027** via root `Directory.Build.props`/`.targets`
  (configs: `Release` = 2020 baseline, `Release R21` … `R27`; frameworks net472 / net48 /
  net8.0-windows / net10.0-windows). **2020 is the tested baseline and the version Ajmal runs live.**
- **A normal build auto-deploys the DLL into the local Revit Addins folders** (ProgramData + AppData).
  For a compile-only check, always pass `-p:SkipAjToolsAutoDeploy=true`.
- Packaging scripts live in `dist/`; releases ship through the separate `AJ-Tools-Installer` GitHub
  repo (see `RELEASE_PROCESS.md`). GitHub/git work goes through the `aj-tools-github` skill.

**Hard rule — trees you must never edit:** `AJ Tools\` (old copy of the project) and `_backup\`
(pre-multiversion snapshot). All source work happens in root `src/` only. Never hand-edit generated
output (`src/bin`, `src/obj`, `dist/release`).

**Git — the root IS the working repository now (changed 2026-08-05).** The root `.git` used to be
hollow (hooks/ and info/ only), so nothing done in the live `src/` tree was recorded anywhere. It was
replaced with a copy of the repository that had been sitting inside `AJ Tools\`, so the root now
carries the full history (183 commits, 47 tags) on branch `master`, pushed to
`https://github.com/Ajmalpshaik/AJ-Tools.git` (private). Normal git commands work at the root.

`AJ Tools\` still contains its own `.git` pointing at the same remote — it was copied, not moved, so
that tree stayed untouched per the do-not-edit rule. **Ignore it: commit from the root only.**
`.gitignore` excludes both `AJ Tools/` and `AJ-Tools-Installer/` (the installer is its own separate
repository). All GitHub/git work still goes through the `aj-tools-github` skill, and pushing still
needs Ajmal's explicit yes.

## Who you're working with, and how

Work here is driven by the skills under `.claude/skills/ajtools-*`, backed by shared knowledge files
in `.claude/knowledge/`: `ajtools-conventions.md`, `glossary.md`, `reply-style.md`, `debug-log.md`, and
the **live-model set** `knowledge/live-model/` — an indexed folder, not one file. Live-model work runs
through the AJ AI Bridge (`mcp__aj-tools-aj-ai__ping` / `run_csharp`) — ping first;
bridge rules are in `knowledge/live-model/core.md`.

**Read by routing, never wholesale (Ajmal's rule, 2026-07-16).** Knowledge is split small and indexed so a
task reads only the part it needs: open the index — [`knowledge/live-model/README.md`](.claude/knowledge/live-model/README.md)
for live-model work, [`scripts/README.md`](.claude/scripts/README.md) for reusable C# — pick the one row
that matches the request, open that one file. Don't read a whole set to find one section. Same rule applies
to anything new: if a knowledge file grows past ~300 lines, split it by topic and add it to its index.

Ajmal is a BIM modeller, not a developer — always reply in plain, non-developer language (reply
formats live in `reply-style.md`), and confirm before any action that's hard to reverse or that
reaches beyond this working folder: changing the live Revit model destructively, deploying over a
live add-in, anything touching GitHub or the installer repo, or deleting/overwriting files you didn't
create.

## Always-on discipline (applies to every AJ Tools task, whether or not a specific skill got invoked)

**AJ Adaptive AI-Local Workflow.** Work as a hybrid system: AI interprets Ajmal's request, routes it to
the right local knowledge/module, fills the inputs, and verifies the result; local `.claude/knowledge`,
`.claude/scripts`, and project code provide the reusable execution. The split is not fixed: a task can be
mostly local reuse, mostly AI reasoning, or anywhere between. The loop is
`request -> route by shape -> compose local modules -> run/check -> answer -> improve the library`, so
repeated work gets faster over time instead of being rewritten from scratch. If no local module fits yet,
AI still solves the task with the smallest correct one-off, verifies it, then saves the reusable part only
when the shape is likely to repeat. Decision order: reuse existing module first; if missing, do the task
normally; after verification, harvest reusable pieces when useful.

For simple live-model count/size checks, do not rebuild the AJ AI Bridge named-pipe caller by hand. If
native `mcp__aj-tools-aj-ai__ping` / `run_csharp` tools are not exposed, use the visible fallback
shortcut `tools\invoke-revit-bridge.ps1` (for example `-Ping` or `-CodeFile <composed-script.cs>`). Plain
`rg --files` ignores dot folders like `.claude`, so this visible wrapper exists to prevent agents missing
the real helper and taking the slow route.

1. **Before starting substantive work**, check the relevant `.claude/knowledge/*.md` file(s) for a term,
   convention, or prior finding that already answers or shapes the request. Don't re-derive or re-trace
   something that's already documented — e.g. the CRAC A↔B refrigerant pairing and the geometric MEP trace
   method are already recorded in `glossary.md` / `knowledge/live-model/mep-trace.md`; re-solving them from scratch is
   wasted work and risks a different (wrong) answer than what was already verified. **Before writing any
   new AJ AI Bridge C#, also check [`.claude/scripts/`](.claude/scripts/README.md)** — most live-model
   requests split into "which elements" (a `filters/` fragment, or a `creators/` fragment if the
   elements don't exist yet and need creating) and "what to do to them" (one or more `actions/`
   fragments), composed together per request rather than written as one bespoke script each time;
   genuinely bespoke multi-stage builds live in `recipes/` instead. Route by request shape first, not by
   file-name searching today's noun (`*duct*`, `*pipe*`, etc.), because the reusable modules are generic
   and may not contain the element name. Adapt what's there before writing from scratch.
2. **After finishing**, check whether anything new surfaced that belongs in exactly one of these places —
   never duplicate the same fact across files:
   - A new AJ Tools coding convention/decision → `ajtools-conventions.md`
   - A new ambiguous term or dictation quirk → `glossary.md`
   - A new AJ AI Bridge/live-model technical gotcha → the matching topic file in `knowledge/live-model/` (route via its README index)
   - A reply-format correction from Ajmal → `reply-style.md`
   - A bug found + fixed → `debug-log.md`
   - A genuinely reusable piece of live-model C# (not a one-off query) → save or update it in
     `.claude/scripts/`, following that folder's own README
   - A fact about Ajmal, an ongoing project decision, or standing feedback that should follow him across
     *all* projects/sessions (not just this repo) → the cross-session memory system
     (`C:\Users\AjmalAlavudheen\.claude\projects\D--Ajmal-Revit-Addins\memory\`), per its own type rules
     (user/feedback/project/reference).
3. **If a task pattern looks recurring, bounded, and not already covered by an existing skill**, create it
   via `ajtools-claude-maker` and report it in the same reply — "created skill X because Y; say delete if
   you don't want it" (Ajmal changed ask-first to create-then-report, 2026-07-16). The report is mandatory —
   silent creation stays forbidden. Because no one gatekeeps anymore, check for overlap with existing
   skills *before* creating, and write what the new skill must NOT fire on. Deleting or replacing an
   *existing* skill still needs Ajmal's explicit OK first.
4. This is a standing habit, not a one-off checklist — it applies regardless of which skill (if any) is
   active, including plain conversation that never invokes a skill at all.

For an on-demand full pass over a whole session (rather than the lightweight per-task check above), use
the `ajtools-knowledge-sync` skill.

## Modeler mindset — verify, don't trust the API/naming at face value

Revit's API output and the model's own naming/tagging conventions describe *intent*, not always *physical
reality*. Don't report either as fact without checking — think like an experienced modeller who'd walk the
run themselves rather than just reading the tag.

- **Proof case**: the CRAC refrigerant trace. Tag names implied `CAC001A` pairs with `ACU001A*`, and
  `Connector.IsConnected` was `false` end to end — both looked like they should be trustworthy, both were
  wrong. The real pairing (A↔B cross-connected) only came out by geometrically tracing actual connector
  positions, ignoring both the naming and the `IsConnected` flag. See `glossary.md` (the pairing) and
  `knowledge/live-model/mep-trace.md` (the trace technique) for the full detail — don't re-derive it, read it first (see
  discipline #1 above).
- **The general habit, for every AJ Tools task** (not just MEP tracing): when the direct/obvious Revit API
  answer doesn't hold up — a flag that should be true isn't, a name that should predict behavior doesn't —
  don't paper over it or guess. Find the technique that gets the *real* answer (geometry, cross-referencing
  a different property, walking the actual model), and report what you actually found even when it
  contradicts what the naming or a surface-level check would suggest.
- **Grow this as a library, not a one-off**: each time a new "trick" like this gets discovered (a case
  where the obvious approach lied and something else revealed the truth), write it down in the knowledge
  file it belongs to (`knowledge/live-model/` — the right topic file — for AJ AI Bridge/live-model techniques, `ajtools-conventions.md`
  for compiled-plugin-side ones) — same routing rule as discipline #2. The point is that the next session
  starts with a bigger toolbox of "here's how you get the real answer when Revit's own data doesn't just
  hand it to you," not that everyone re-invents the same trick each time.

## Definition of done — run these checks before calling any work finished

**Method, always**: plan first, split into visible steps, execute step by step, verifying each step
before starting the next (Ajmal confirmed this way of working) — never one opaque action.

**Plugin source work** (`ajtools-build` / `ajtools-debug`):
1. House conventions followed — metadata block, version-safe helpers, `LeaderLogicService` for tag
   leaders, ribbon wiring in the ribbon managers. The checklist with detail is `ajtools-conventions.md`.
2. Clean build, deploy skipped:
   `msbuild "AJ Tools.sln" -p:Configuration=Release -p:Platform=x64 -p:SkipAjToolsAutoDeploy=true`
   → zero errors, zero warnings (the project currently builds warning-free — keep it that way).
   `Release` (2020) is the minimum; if the change touches version-sensitive API, also build at least
   one newer config (e.g. `Release R25`) before calling it done.
3. Suite version decision made in `src/Properties/AssemblyInfo.cs` — patch bump for a fix, minor for a
   new tool.
4. Report honestly what was and wasn't tested. If Revit wasn't launched, say so plainly — "builds
   clean, not yet loaded in Revit" — never imply live testing that didn't happen.

**Live-model work** (AJ AI Bridge): verify the result against the real model with a fresh read-back —
never trust `IsConnected`, naming, or an earlier turn's state (see Modeler mindset). All numbers to
Ajmal in mm.

**Everything, always**: knowledge capture done (discipline #2), and scope held — never grow a small
fix into a project-wide refactor (e.g. the pending `ElementIdHelper` migration across old files)
without Ajmal's explicit sign-off.

## Quality floor — non-negotiables for whichever model is running this repo

Nothing here is new; it's the habits above restated as mechanical rules, because they are exactly the
steps a model tends to skip under time or capability pressure — and skipping them is what produces
wrong answers in this repo. Follow them even when the task looks trivially simple:

1. **Read first, act second.** Relevant `.claude/knowledge/*.md` file(s) before substantive work. An
   ambiguous or garbled term (Ajmal dictates a lot) → check `glossary.md`, then ask — one short
   question with a recommended default — rather than guessing.
2. **Never answer a Revit API question from memory.** Verify in the knowledge files, live via
   AJ AI Bridge, or against the real installed `RevitAPI.dll` (`C:\Program Files\Autodesk\`). NuGet
   reference packages and web docs have both been proven wrong here.
3. **Plan → split → execute.** Show a short numbered plan, run one step at a time, check each step's
   real result before starting the next. Never one opaque script that does everything at once.
4. **Fresh reads, not recall.** Never trust `Connector.IsConnected`, element names/tags, or your own
   earlier turn's results — Ajmal undoes and edits things in Revit between messages. Re-query before
   acting on "known" state, and verify what you changed with a read-back after.
5. **Every number is per-request.** Clearances, flows, heights, margins — confirm fresh and restate
   before calculating; never reuse a past session's value as a default. He speaks in mm; Revit's API is
   feet; convert explicitly and reply in mm.
6. **Reply format.** Plain non-developer language, always. Count question → bare number, one line.
   Size/breakdown → schedule-style table (`Size (mm) | Qty`). Detail only when asked
   (`reply-style.md` rules win).
7. **Confirm before**: bulk or hard-to-reverse model changes (state what will happen and how many
   elements), anything destructive, anything touching GitHub/releases/deployment. Small, easily-undone
   changes: just do them and report. A vague "let me check" from Ajmal is not a go-ahead.
8. **"Mistake" / "undo" / "previous"** → Revit's native Undo command via the bridge, never a
   hand-written delete script. If he says he already undid it himself, believe him and re-query.
9. **Compiled plugin work**: metadata block, compat helpers, `LeaderLogicService` for leaders, ribbon
   wiring in the ribbon managers, clean build with `-p:SkipAjToolsAutoDeploy=true` — then run the
   Definition of done above.
10. **Capture or it didn't happen.** New fact → exactly one knowledge file (routing table in
    discipline #2). Recurring task pattern → create the skill via `ajtools-claude-maker` and report it in
    the same reply (create-then-report, Ajmal's 2026-07-16 rule); never without the report. Deleting or
    replacing an existing skill still needs his OK.
11. **Report honestly.** If something failed, wasn't live-verified, or contradicts what the naming/tags
    suggest — say exactly that. A confident wrong answer costs Ajmal real model damage; an honest "built
    but not tested in Revit" costs nothing.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

**Scope — do not lose this.** The graph deliberately covers the LIVE trees only (`src/`, `.claude/`,
`mcp-server/`, `docs/`, `AJ-Tools-Installer/`, `.agents/`, root docs) and **excludes `AJ Tools\` and
`_backup\`**. Those are the stale copies; including them makes the graph half-duplicate and wrong
(`MepOpeningService` appeared twice, `AJTools.Utils` reported 315 edges instead of its real 162, and
communities came out in mirrored pairs). A plain `graphify update .` re-scans the whole repo and will
pull the stale tree back in — filter `AJ Tools` and `_backup` out of the detect file list before
extracting. Rescoping is nearly free: the cache under `graphify-out/cache/` made the 2026-08-04 rebuild
cost zero new tokens. There is no git hook: the root `.git` is hollow, so `graphify hook install` cannot
run here — this CLAUDE.md section plus the `.claude/settings.json` PreToolUse guards do that job instead
(remove with `graphify claude uninstall`).
