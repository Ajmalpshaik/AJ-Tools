# AJ Connect — tool sources (PRIVATE)

The C# behind every tool published to AJ Connect. **This folder is deliberately in the private
AJ-Tools repository, not in the public AJ-Connect one.**

## Why it lives here

A `.ajtool` file carries the tool's code as plain text. Anyone who can reach the tools folder on the
website can read it — that is unavoidable, because AJ Connect has to read it to run it, and no amount
of obfuscation changes that.

What *is* avoidable is putting the readable source in a **public, browsable repository** where it is
indexed, searchable and trivially found. The public repo holds the engine; the tools stay here.

This was got wrong once (2026-08-12): `tools-source/` was committed to the public AJ-Connect repo
along with everything else. It only held Unhide All, which matters to nobody — but it was a folder
that would have quietly collected real work. Ajmal spotted it in the GitHub file list. Moved here the
same day.

## How to publish

From the AJ Connect working copy (`D:\Ajmal\AJ Connect`):

```powershell
# to the website folder, ready to upload
powershell -ExecutionPolicy Bypass -File tools\publish-tools.ps1 -SourceDir "D:\Ajmal\Revit Addins\connector-tools" -ForUpload

# or straight to this machine's local folder, for testing
powershell -ExecutionPolicy Bypass -File tools\publish-tools.ps1 -SourceDir "D:\Ajmal\Revit Addins\connector-tools"
```

## Adding a tool

1. Write the C# in a new `.cs` file here
2. Add an entry to `tools.json` — `id`, `name`, `panel`, `description`, and the `code` file name
3. Publish with the command above
4. Upload the contents of `dist\publish\` to the website

Colleagues press **Check for new tools**. Nobody reinstalls anything.

## Writing a tool — what the script gets

Available by name: `Document`, `UIDocument`, `Application`, `UIApplication`.
Already imported: `System`, `System.Linq`, `System.Collections.Generic`, `Autodesk.Revit.DB`,
`Autodesk.Revit.UI`.

Rules that bite:

- **End with an explicit `return`.** A trailing expression without a semicolon does not reliably
  produce output in this hosting setup.
- **Return the report as a string.** It appears on the web page. Never show a `TaskDialog` — the
  person is looking at a browser, and a dialog would appear on the Revit screen and block Revit until
  somebody walked over to it.
- **Open your own transaction.** AJ Connect wraps the whole tool in a transaction group, so the
  result is one undo step, but the tool owns its own transactions.
- **Speak mm.** Revit's API is in feet. Convert explicitly and report in mm.
