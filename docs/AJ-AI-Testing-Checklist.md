# AJ AI — Revit Testing Checklist

**Tool:** AJ AI (Gemini Shell) — embedded AI code assistant
**Change:** Safety & stability hardening pass
**Suite build to test:** AJ-Tools (Release, Revit 2020)
**Date prepared:** 2026-07-01
**Tested by:** _________________  **Test date:** _________________

> ⚠️ This checklist has **not** been run in Revit by the developer. It is for **you** to test in a live Revit 2020 session and mark each row **Pass / Fail**. Test on a **throwaway/sample model first** — some steps deliberately try destructive actions.

---

## A. Setup / loading

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| A1 | Restart Revit 2020 after the new build was deployed | AJ-Tools ribbon loads with no startup error popup | ☐ |
| A2 | Click the **AJ AI** button on the ribbon | The AI panel opens docked on the right | ☐ |
| A3 | Look at the top bar of the panel | Shows e.g. **"Gemini: API key configured"** (or "…API key missing — open Settings") — but **never shows the actual key** | ☐ |
| A4 | Open **⚙ Settings**, switch provider between Gemini and OpenAI | Top-bar status updates to match the selected provider | ☐ |

## B. Normal use (should work exactly like before)

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| B1 | Type a simple read-only request (e.g. *"count the ducts in the active view"*) → **Generate C# Code** | C# code appears in the editor, first line is `// Name: ...` | ☐ |
| B2 | Click **▶ Run Code** | Output console shows a summary (e.g. "X ducts found"); model is unchanged | ☐ |
| B3 | Click **🧹 Format Code** on the generated code | Code re-indents cleanly, nothing is deleted or broken | ☐ |
| B4 | Click **Review Code** | AI feedback text appears in the output console | ☐ |
| B5 | Click **💾 Save Script** | Status shows the **full saved path**; script appears under "Saved Scripts History" | ☐ |
| B6 | Hover the saved script in history | Tooltip shows your prompt **and** which provider generated it | ☐ |

## C. New safety behaviour (the point of this update)

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| C1 | Ask for a **destructive** action (e.g. *"delete the selected elements"*), Generate, then **Run** | A **Yes/No confirmation popup** appears first, warning it can only be undone with Ctrl+Z | ☐ |
| C2 | On that popup, click **No** | Nothing is deleted; output says the run was cancelled | ☐ |
| C3 | Repeat C1 and click **Yes** (on sample model!) | Elements are deleted; **one** Ctrl+Z fully reverses it | ☐ |
| C4 | Paste code that writes/deletes a file on disk (e.g. `File.Delete(...)`) into the editor → **Run** | Run is **BLOCKED** before executing; output explains why in plain language | ☐ |
| C5 | Paste code that opens a web address (`new HttpClient()...`) → **Run** | Run is **BLOCKED**; output says it tried network access | ☐ |

## D. Stability (concurrency, stop, retry)

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| D1 | Run a longer script, and while it's running, all action buttons (Generate/Run/Review/Format/Save) | are **greyed out** until it finishes | ☐ |
| D2 | Run a script that loops many times, then click **⏹ Stop** | Script actually stops; output says "Stopped by the user" | ☐ |
| D3 | Generate code that fails on purpose (e.g. a wrong parameter name), then **Run** | It auto-asks the AI to fix and retries, showing **attempt numbers** | ☐ |
| D4 | If the same error keeps coming back | It **stops early** saying "the same error repeated" instead of trying all 5 times | ☐ |
| D5 | After a clean run, close and reopen the AJ AI panel | Panel reopens normally, saved history still listed | ☐ |

## E. Logging (optional check)

| # | Step | Expected result | Pass / Fail |
|---|------|-----------------|-------------|
| E1 | After a few runs, open `%AppData%\AJTools\AiShell_Activity.log` in Notepad | One line per run: provider, script name, success, attempts, short prompt/error — **no API key**, no model data | ☐ |

---

## Notes / issues found

_Write anything that failed or behaved oddly here, with the request text you typed:_

```
(your notes)
```

## Known limitations (already documented — not bugs to report)
- The safety scan is a **pattern check, not a full sandbox** — cleverly disguised code could still slip past. Always glance at generated code before running on a real model.
- A script stuck in a **single long Revit operation** (not a loop) can't be interrupted by Stop — only loop-based scripts and a 60-second backstop can.
- AJ AI targets **Revit 2020 only** for now.
