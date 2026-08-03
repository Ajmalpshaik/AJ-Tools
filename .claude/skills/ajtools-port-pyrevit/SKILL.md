---
name: ajtools-port-pyrevit
description: Convert one of Ajmal's existing pyRevit tools (from D:\Ajmal\AutoCAD APP\Pyrevit\AJ-Tools.extension) into a proper AJ Tools C# command — same behaviour, house conventions, ribbon button and all. Use whenever Ajmal points at a pyRevit pushbutton/script and wants it in AJ Tools: "convert this tool to C#", "this is my pyRevit tool, make it in AJ Tools", "using this script build the same in the plugin", "study how I achieve this" (with a .py path or a .pushbutton folder), or broken-English/dictated versions ("convert this pyrevit tool", "make this in c sharp"). Also use when a paste of IronPython/pyRevit code arrives with intent to port it. His pyRevit scripts encode hard-won, WORKING logic — the port must reproduce it exactly, not "improve" it silently. Do NOT use this for writing new pyRevit/Python tools (that's the revit-pyrevit-python skill) or for brand-new C# capability with no existing script to port (that's ajtools-build). Do NOT use this for debugging an already-ported tool — that's ajtools-debug.
---

# AJ Tools — Port a pyRevit Tool to C#

Ajmal has a working pyRevit extension (`D:\Ajmal\AutoCAD APP\Pyrevit\AJ-Tools.extension`) that predates
AJ Tools, and he migrates tools from it one at a time ("Colorize" and a Debug-panel tool were both done by
hand before this skill existed). The key mindset: **the pyRevit script is the specification.** It already
works in production — its logic came from real trial and error (the duct end-cap recipe, ported from his
pyRevit tool after two "simpler" C# attempts failed, is the proof case). Port faithfully; propose
improvements separately.

## How to work: plan, split, then execute

### Step 1 — Read the knowledge files

[`ajtools-conventions.md`](../../knowledge/ajtools-conventions.md) (house conventions the ported command
must follow) and [`glossary.md`](../../knowledge/glossary.md) for any ambiguous term in the request.

### Step 2 — Locate and study the source tool

Find the pushbutton bundle (`script.py`, `icon.png`, any `bundle.yaml`/`lib` files) — under
`D:\Ajmal\AutoCAD APP\Pyrevit\AJ-Tools.extension` unless Ajmal gives a different path. Read the whole
script, then **play the behaviour back to Ajmal in plain modeller words before writing any C#**: what the
tool asks the user, what it collects, what it changes, what it shows at the end. This is the cheap moment
to catch a misread — not after 300 lines of C#. If the script has dead code or commented-out experiments,
ask which behaviour is the live one rather than guessing.

### Step 3 — Map it to house style

Translate, keeping behaviour identical but the *implementation* native to AJ Tools:

- IronPython/pyRevit idioms → plain Revit API C#. Watch the classic traps: pyRevit's `revit.doc`
  globals → the command's `ExternalCommandData`; Python duck-typing → explicit casts; `pyrevit.forms`
  dialogs/output windows → `DialogHelper` / an existing WPF pattern from `src/UI`; script-global state →
  proper fields.
- House conventions apply in full: `#region Metadata` block (note the pyRevit origin in it), version-safe
  compat helpers (`ElementIdHelper`, `RevitCompat`, `TagCompat`, `FilterRuleCompat`) instead of raw
  version-sensitive calls — the script only ever ran on one Revit version, the C# builds for 2020–2027 —
  transaction naming `"AJ Tools - <Tool>"`, single-undo grouping, no success popups, confirmation before
  bulk edits.
- Ribbon: register the button in `Core/RibbonManager.cs` / `Core/AnnotationRibbonManager.cs` on the panel
  Ajmal names (ask if he didn't), reusing the pyRevit bundle's icon if its size/format fits the existing
  icon set.

### Step 4 — Verify against the original

1. Build clean with deploy skipped:
   `msbuild "AJ Tools.sln" -p:Configuration=Release -p:Platform=x64 -p:SkipAjToolsAutoDeploy=true` —
   zero errors, zero warnings.
2. If the AJ AI Bridge is connected (`mcp__aj-tools-aj-ai__ping`), verify the ported logic's
   observable effect against the live model — where practical, compare with what the pyRevit tool itself
   produces on the same elements (Ajmal can run it; the bridge can't click buttons). The cap-end port was
   only trusted after exactly this kind of check caught two real gaps (`IsConnected == true` with wrong
   size AND wrong rotation).
3. Report honestly which parts were live-verified and which are build-only.

### Step 5 — Close out

- Ask Ajmal whether the pyRevit original stays (both alive) or should be retired from the extension — his
  call, don't touch the extension folder either way without it.
- Suite version: new tool in AJ Tools = minor bump in `src/Properties/AssemblyInfo.cs` + changelog entry.
- Append a dated line to `ajtools-conventions-log.md` (what was ported, anything learned). A new
  IronPython→C# translation trap worth remembering goes to `ajtools-conventions.md`'s conventions; a
  live-model gotcha found while verifying goes to the matching topic file under `knowledge/live-model/`
  (route via its [`README.md`](../../knowledge/live-model/README.md)) — one fact, one file.
