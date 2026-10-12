# C# Pane — Revit Testing Checklist

**Tools:** the **C#** pane (AI Assistant panel), **Run Pinned / Saved Scripts**, plus the Game Mode and
Quick Menu fixes made in the same pass
**Suite build to test:** AJ Tools 1.57.1 (Revit 2020 first, then any newer version you use)
**Date prepared:** 2026-10-12 (replaces the 2026-07-01 checklist)
**Tested by:** _________________  **Test date:** _________________

> ⚠️ None of this has been run in Revit by the developer: the code builds clean for Revit 2020–2027,
> nothing more. Test on a **throwaway/sample model first** — some steps deliberately try risky actions.
> The pane's dialogs still use its older name "AJ AI" in their titles; that is expected.

---

## A. Setup / loading

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| A1 | Restart Revit after installing the new build | AJ Tools loads with no startup error popup | ☐ |
| A2 | Click **C#** on the AI Assistant panel | The pane shows, docked | ☐ |
| A3 | Look at the top bar of the pane | Shows the provider and "API key configured" (or "missing — open Settings") — **never the key itself** | ☐ |
| A4 | **⚙ Settings** → switch between Gemini, OpenAI, Claude and NVIDIA | The top-bar status follows the provider picked | ☐ |

## B. Normal use

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| B1 | Type a read-only request (e.g. *"count the ducts in the active view"*) → **Generate C# Code** | Code appears in the editor | ☐ |
| B2 | **▶ Run Code** | The output shows the result; the model is unchanged | ☐ |
| B3 | **Format Code** | Code re-indents; nothing deleted | ☐ |
| B4 | **Review Code** | Review text appears in the output | ☐ |
| B5 | **Save Script** | The status line shows the full saved path; the script is listed under Saved Scripts | ☐ |
| B6 | Paste a script that declares its own class with a method (e.g. `class Helper { public static int Twice(int x) { return x * 2; } }` then `Helper.Twice(4)`) → **Run** | **Runs.** It used to fail to compile with error CS0120 | ☐ |

## C. Safety

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| C1 | Ask for a destructive action (e.g. *"delete the selected elements"*) → Generate → **Run** | A Yes/No confirmation appears first | ☐ |
| C2 | Click **No** | Nothing deleted; output says the run was cancelled | ☐ |
| C3 | Repeat, click **Yes** (sample model!) | Elements deleted; **one** Ctrl+Z reverses it | ☐ |
| C4 | Paste code that deletes a file on disk (`File.Delete(...)`) → **Run** | **Blocked** before running, with a plain reason | ☐ |
| C5 | Paste code that opens a web address (`new HttpClient()`) → **Run** | **Blocked**, saying it tried network access | ☐ |
| C6 | **Review Code** on a script with two different risky findings | It asks again for each **new** finding, not only the first one | ☐ |

## D. Stability

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| D1 | While a long script runs | Generate / Run / Review / Format / Save are greyed out until it finishes | ☐ |
| D2 | Run a script that loops many times → **⏹ Stop** | It stops; output says it was stopped | ☐ |
| D3 | Generate code that fails on purpose (wrong parameter name) → **Run** | It asks the AI for a fix and retries, showing attempt numbers | ☐ |
| D4 | Same, with **no API key** set for the provider | It stops with "No API key - auto-fix skipped" instead of trying | ☐ |

## E. Fixed on 2026-10-12 — please confirm each

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| E1 | Ask for something the AI service refuses or blocks | "The AI returned no code - (its reason)"; **the editor keeps what was in it** (it used to be wiped) | ☐ |
| E2 | Settings → change the scripts folder to a folder you cannot write to → **Save Script** | "Could not save the script: …" and the pane is usable straight after | ☐ |
| E3 | Pin a script (Saved Scripts), then change the provider or the key in Settings, then **Run Pinned** | The pinned script still runs (the pin used to be lost) | ☐ |
| E4 | Pin a script that loops forever → **Run Pinned** | Stops at the time limit with the same message the pane gives | ☐ |
| E5 | Settings → type a different key → **Close** (not Save) → reopen Settings | The old, saved key is shown — an unsaved edit is dropped | ☐ |
| E6 | Advanced: close Revit, open `%AppData%\AJTools\AiShellConfig.json` in Notepad, break it (delete a bracket), save, start Revit | The pane says once that the file could not be read and names a `.bak` copy kept beside it | ☐ |
| E7 | Gemini with no model typed in Settings → **Generate** | Works — a current Gemini model is picked automatically, or the reason it could not list models is shown | ☐ |
| E8 | Revit 2024 or newer: ask for a script that sets a length | Uses the current unit calls for that Revit version, so it compiles first time more often | ☐ |

## F. Game Mode and Quick Menu (same pass)

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| F1 | Game Mode: mark a snag, then exit with Esc, Esc | The end text says where the snag report was saved | ☐ |
| F2 | Game Mode → Key Settings: swap two keys, close Revit, start again | Every action still has its key (one could end up with none) | ☐ |
| F3 | Quick Menu → Customise → **Save** | Saves as before; if it cannot, a red line says so and the window stays open | ☐ |

## G. Logging (optional)

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| G1 | After a few runs open `%AppData%\AJTools\AiShell_Activity.log` | One line per run: provider, script name, success, attempts — **no API key**, no model data | ☐ |

---

## Notes / issues found

_Write anything that failed or behaved oddly, with the request text you typed:_

```
(your notes)
```

## Known limitations (not bugs to report)
- The safety scan is a **pattern check, not a sandbox** — always glance at generated code before running it
  on a real model.
- A script stuck inside a **single long Revit operation** (not a loop) cannot be interrupted by Stop — only
  loops, and the time-limit backstop, can.
- A loop inside a class the script declares is not checkpointed; only the overall time limit stops it.
- Two Revit sessions open at once each keep their own copy of the pane's settings; the last one to save wins.
