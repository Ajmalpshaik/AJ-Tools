# AJ Tools — Working Agreement

## What this project is

AJ Tools is Ajmal's own Revit add-in: a C# / WPF plugin that adds two ribbon tabs ("AJ Tools" and
"AJ Annotation") of view, graphics, datum, MEP, coordination, annotation, dimensioning, and tagging
tools, plus a built-in AI shell (AJ AI). Facts a session should not have to rediscover:

- Solution: `AJ Tools.sln` at the repo root. **Live source tree: root `src/`** (`src/AJ Tools.csproj`) —
  Commands, Core (ribbon managers), Helpers, Models, Services, UI, AiShell, GameMode, QuickMenu, Resources.
- One codebase builds **Revit 2020 → 2027** via root `Directory.Build.props`/`.targets`
  (configs: `Release` = 2020 baseline, `Release R21` … `R27`; frameworks net472 / net48 /
  net8.0-windows / net10.0-windows). **2020 is the tested baseline and the version Ajmal runs live.**
- **A normal build auto-deploys the DLL into your own Revit Addins folder** (AppData, a fresh
  `AJ Tools.<stamp>` folder per build; all-users installs only ever go through `dist\install-all-users.cmd`).
  For a compile-only check, always pass `-p:SkipAjToolsAutoDeploy=true`.
- Packaging scripts live in `dist/`; releases ship through the separate `AJ-Tools-Installer` GitHub
  repo (see `RELEASE_PROCESS.md`). GitHub/git work goes through the `github-aj-tools` skill.
- **Never add a `connector/` or `connector-tools/` folder under this root.** Both are gitignored:
  a separate product once lived here and was moved out for exactly the reason the old `AJ Tools\`
  tree demonstrated — two copies drift.
- **AJ Tools does not serve a web page, and must not grow one back without Ajmal asking.** A built-in
  Web Panel shipped in v1.43.0 (a ribbon button starting a localhost HTTP server that ran ribbon tools
  from a browser) and was **removed at Ajmal's instruction in v1.44.0** — code deleted, not disabled,
  the same way the spoken voice went in v1.42.0. What was learned building the panel is kept as lessons in
  `.claude/knowledge/ajtools-conventions.md`; treat that section as history, not as a live design.
- **No outside-AI connection either (removed v1.57.0, 2026-10-12).** The AJ AI Bridge (named pipe,
  `McpBridgeService`, the "AJ AI" ribbon button), its Node MCP server and the PowerShell callers were all
  deleted at Ajmal's instruction; that job now belongs to his separate Heron AI project — never touch that
  project from here. The C# pane (with its AI chat, for now), Run Pinned / Saved Scripts, Game Mode and
  Quick Menu stay. Do not add a bridge back without Ajmal asking. The live-model notes and C# scripts that
  went with it were deleted the same day (Heron already covers that work) — git history has them.

**Hard rule — trees you must never edit:** `_backup\`
(pre-multiversion snapshot). All source work happens in root `src/` only. Never hand-edit generated
output (`src/bin`, `src/obj`, `dist/release`).

**Git — the root IS the working repository now (changed 2026-08-05).** The root `.git` used to be
hollow (hooks/ and info/ only), so nothing done in the live `src/` tree was recorded anywhere. It was
replaced with a copy of the repository that had been sitting inside `AJ Tools\`, so the root now
carries the full history on branch `master`, pushed to
`https://github.com/Ajmalpshaik/AJ-Tools.git` (**public** — checked 2026-08-20; this line said "private"
until then, and it was wrong, which matters when deciding what may be written into a commit). Normal git
commands work at the root.

The old `AJ Tools\` copy (with its own `.git`) was **deleted on 2026-10-12** after the move to the new PC —
checked first: same commit as GitHub, nothing uncommitted. If a folder of that name ever reappears, it
is stale: do not edit it, commit from the root only.
`.gitignore` excludes both `AJ Tools/` and `AJ-Tools-Installer/` (the installer is its own separate
repository). All GitHub/git work still goes through the `github-aj-tools` skill, and pushing still
needs Ajmal's explicit yes.

## Who you're working with, and how

Work here is driven by the skills under `.claude/skills/ajtools-*`, backed by shared knowledge files
in `.claude/knowledge/`: `ajtools-conventions.md` and `debug-log.md`.
Nothing in this repo can reach the live Revit model (see above) — when something needs checking in Revit,
say so and ask Ajmal to test it himself.

**Read by routing, never wholesale (Ajmal's rule, 2026-07-16).** Read only the part of a knowledge file a
task needs (search it, then open that section). If a knowledge file grows past ~300 lines, split it by topic.

Ajmal is a BIM modeller, not a developer — always reply in plain, non-developer language (see
quality floor #6), and confirm before any action that's hard to reverse or that
reaches beyond this working folder: deploying over a live add-in, anything touching GitHub or the installer repo, or deleting/overwriting files you didn't
create.

## Always-on discipline (applies to every AJ Tools task, whether or not a specific skill got invoked)

1. **Before starting substantive work**, check the relevant `.claude/knowledge/*.md` file(s) for a term,
   convention, or prior finding that already answers or shapes the request. Don't re-derive something
   that's already documented and verified — re-solving it from scratch is wasted work and risks a
   different (wrong) answer. Reuse the plugin's existing helpers and services before writing new ones.
2. **After finishing**, check whether anything new surfaced that belongs in exactly one of these places —
   never duplicate the same fact across files:
   - A new AJ Tools coding convention/decision → `ajtools-conventions.md`
   - A reply-format correction from Ajmal → quality floor #6 in this file
   - A bug found + fixed → `debug-log.md`
   - A fact about Ajmal, an ongoing project decision, or standing feedback that should follow him across
     *all* projects/sessions (not just this repo) → the cross-session memory system
     (`C:\Users\ajmal\.claude\projects\D--06-Development-01-AJ-Tools-Revit-Addins\memory\`), per its own type rules
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

- **Proof case (model data)**: a refrigerant trace where tag names implied one pairing and
  `Connector.IsConnected` was `false` end to end — both looked trustworthy, both were wrong. The real
  pairing only came out by geometrically tracing actual connector positions.
- **Proof case (plugin code, found 2026-10-12)**: L-Shape Leader read and wrote tag leaders by
  reflection on property names. Revit 2023 removed those properties from `IndependentTag`, the build
  still compiled clean on all eight versions, and the tool silently did nothing on MEP tags on Revit
  2023 and later. Only reading the installed `RevitAPI.dll`/`RevitAPI.xml` per version showed it. A
  clean build proves the code compiles, not that it still does the job.
- **The general habit, for every AJ Tools task** (not just MEP tracing): when the direct/obvious Revit API
  answer doesn't hold up — a flag that should be true isn't, a name that should predict behavior doesn't —
  don't paper over it or guess. Find the technique that gets the *real* answer (geometry, cross-referencing
  a different property, walking the actual model), and report what you actually found even when it
  contradicts what the naming or a surface-level check would suggest.
- **Grow this as a library, not a one-off**: each time a new "trick" like this gets discovered (a case
  where the obvious approach lied and something else revealed the truth), write it down in the knowledge
  file it belongs to (`ajtools-conventions.md`) — same routing rule as discipline #2. The point is that the next session
  starts with a bigger toolbox of "here's how you get the real answer when Revit's own data doesn't just
  hand it to you," not that everyone re-invents the same trick each time.

## Definition of done — run these checks before calling any work finished

**Method, always**: plan first, split into visible steps, execute step by step, verifying each step
before starting the next (Ajmal confirmed this way of working) — never one opaque action.

**Plugin source work** (`ajtools-build` / `ajtools-debug`):
1. House conventions followed — metadata block, version-safe helpers, `LeaderLogicService` for tag
   leaders, ribbon wiring in the ribbon managers. The checklist with detail is `ajtools-conventions.md`.
2. Clean build, deploy skipped (this PC has the .NET 10 SDK and Visual Studio 2026; VS's `msbuild` is not
   on PATH, so use `dotnet build`):
   `dotnet build "AJ Tools.sln" -c Release -p:Platform=x64 -p:SkipAjToolsAutoDeploy=true --no-incremental`
   → zero errors, zero warnings (the project currently builds warning-free — keep it that way).
   `Release` (2020) is the minimum; if the change touches version-sensitive API, also build at least
   one newer config (e.g. `Release R25`) before calling it done.
3. Suite version decision made in `src/Properties/AssemblyInfo.cs` — patch bump for a fix, minor for a
   new tool.
4. Report honestly what was and wasn't tested. If Revit wasn't launched, say so plainly — "builds
   clean, not yet loaded in Revit" — never imply live testing that didn't happen.

**Everything, always**: knowledge capture done (discipline #2), and scope held — never grow a small
fix into a project-wide refactor (e.g. the pending `ElementIdHelper` migration across old files)
without Ajmal's explicit sign-off.

## Quality floor — non-negotiables for whichever model is running this repo

Nothing here is new; it's the habits above restated as mechanical rules, because they are exactly the
steps a model tends to skip under time or capability pressure — and skipping them is what produces
wrong answers in this repo. Follow them even when the task looks trivially simple:

1. **Read first, act second.** Relevant `.claude/knowledge/*.md` file(s) before substantive work. An
   ambiguous or garbled term (Ajmal dictates a lot) → ask — one short
   question with a recommended default — rather than guessing.
2. **Never answer a Revit API question from memory.** Verify in the knowledge files or against the
   real installed `RevitAPI.dll` (`C:\Program Files\Autodesk\`). NuGet
   reference packages and web docs have both been proven wrong here.
3. **Plan → split → execute.** Show a short numbered plan, run one step at a time, check each step's
   real result before starting the next. Never one opaque script that does everything at once.
4. **Fresh reads, not recall.** Never trust `Connector.IsConnected`, element names/tags, or your own
   earlier turn's results — in tool code too: read the real state, and re-read the files and git state
   before acting on what an earlier turn found.
5. **Every number is per-request.** Clearances, flows, heights, margins — confirm fresh and restate
   before calculating; never reuse a past session's value as a default. He speaks in mm; Revit's API is
   feet; convert explicitly and reply in mm.
6. **Reply format.** Plain non-developer language, always; short, detail only when asked. After real
   work (a build, fix or check), end with: what was done, what was and wasn't tested (built only, or
   tried in Revit), what still needs Ajmal's decision, and one next step. Sizes in mm, sorted smallest
   first, as a schedule-style table when there are several.
7. **Confirm before**: anything destructive or hard to reverse, anything touching GitHub/releases/deployment. Small, easily-undone
   changes: just do them and report. A vague "let me check" from Ajmal is not a go-ahead.
8. **Compiled plugin work**: metadata block, compat helpers, `LeaderLogicService` for leaders, ribbon
   wiring in the ribbon managers, clean build with `-p:SkipAjToolsAutoDeploy=true` — then run the
   Definition of done above.
9. **Capture or it didn't happen.** New fact → exactly one knowledge file (routing table in
    discipline #2). Recurring task pattern → create the skill via `ajtools-claude-maker` and report it in
    the same reply (create-then-report, Ajmal's 2026-07-16 rule); never without the report. Deleting or
    replacing an existing skill still needs his OK.
10. **Report honestly.** If something failed, wasn't live-verified, or contradicts what the naming/tags
    suggest — say exactly that. A confident wrong answer costs Ajmal real model damage; an honest "built
    but not tested in Revit" costs nothing.
11. **Never name an outside source in anything written into this repo** — no other people's repos,
    tools, products, websites or personal names, in code comments, knowledge files, changelogs or
    commit messages. Ajmal's instruction, 2026-08-20: *"do not mention any thing that we took from this
    web site or repo... the words also remove... remove his name and do not use like that."* If a
    technique is worth having, write it in your own words as this project's own knowledge and verify it
    here — do not record where it came from, and do not add a file whose subject is somebody else's
    tool. Everything of that kind was stripped on 2026-08-20; putting any of it back undoes a
    deliberate decision. AJ Tools' own command names (Smart MEP Tags, L-Shape Leader, Fix Tag Clash…)
    are Ajmal's and are unaffected.
