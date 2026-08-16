# Usage Guide

## Ribbon Groups

The add-in registers **two** ribbon tabs (panel order as built by `Core/RibbonManager.cs` and
`Core/AnnotationRibbonManager.cs`):

### AJ Tools

- View: View Crop, Unhide All, Toggle Links, Filter Pro, Colorize, Highlight Selection
- Graphics: Apply Graphics, Match Graphics, Reset Graphics
- Datums: Reset Grid/Level Extents to 3D, Modify Level Extents, Flip Grid/Level Bubbles
- Modify: Match MEP Element Elevation, Reassign Reference Level, Pin/Unpin Elements, Smart Selection
- MEP: Connect MEP Elements, Elements to Ceiling Grid, HVAC Schematic, Pipe Sizing
- Opening: MEP Openings (create openings, opening settings)
- Coordination: Element ID lookup, 3D Views by Workset, Link Workset
- Data: Assign Location, Duct Standard
- Manage:
  - Transfer: View Templates, Schedules, Legends, Drafting Views
  - Purge Unplaced: 3D Views, Sections, Schedules, Legends, Drafting Views
  - Purge Unused: View Templates, Filters, Groups
  - Purge Family Parameters
- Family: Shared to Family
- AI Assistant: AJ AI (C# chat pane), AJ AI bridge toggle, Run Pinned / Saved Scripts
- Game: Game Mode
- About: About

### AJ Annotation

- Dimensions: Auto MEP Dimension, Automatic Dimension, Automatic Grid Dimensions,
  Automatic Level Dimensions, Quick Dimension, Copy Dimension Text
- Annotation: Duct Flow Annotations (+ settings), Revision Clouds, Revision Clouds by Elements
  (+ settings), Copy/Swap Text Notes, Copy Text Notes
- Family: Center Annotation
- Tags: Smart MEP Tags (+ settings), Create Tags / Stack Tags (+ Create Tags settings),
  Rearrange Tags (+ Arrange Tags settings), L-Shape Leader, Center Room Tags,
  Section Mark Visibility
- Text: Arrange Text in Box

## Tagging tools - which one to use

Four tools place or move tags. They overlap, so the difference matters:

| Tool | Starts from | What one click does |
| --- | --- | --- |
| Smart MEP Tags | Elements in the view | Tags everything eligible in one go, automatically |
| Create Tags | Selected elements | You click a spot per element; nearest untagged one is tagged |
| Stack Tags | Selected elements | One click tags them all, arranged in a vertical stack |
| Rearrange Tags | Tags that already exist | Moves existing tags into a stack; creates nothing |

Create Tags and Stack Tags share one eligibility rule set, so they cannot drift apart: an element is
skipped if it is already tagged in the view, shorter than the configured minimum length, or a vertical
run (duct, pipe or cable tray). Category list and minimum length live in Create Tags Settings. Stack
spacing comes from Arrange Tags Settings.

## Game Mode

A first-person walkthrough of the model, inside a real Revit perspective view called "AJ Game View"
(created once, then reused). Because it is a genuine Revit view, everything you already use to control
a view shapes the game - visibility/graphics, filters, hide/isolate, section box, display style. That
includes collision: you can only walk into what is actually visible.

Movement is WASD plus mouse-look, with gravity, stair step-up, Shift to sprint and Space to jump. Doors
open with E when you are near one; windows are climbed by jumping. F switches to free flight, G passes
through everything, R respawns, C crouches, and +/- change walking speed.

Right-click cycles five tools:

| Weapon | What it does |
| --- | --- |
| Gun | Shoots - purely visual |
| Laser | Live distance in mm plus the element's identity, system and level |
| Cleaner | Temporarily hides the element hit (U restores) |
| Snag | Marks the element red and adds it to a punch list |
| Selector | Adds the element to Revit's real selection, which survives leaving the game |

Other keys: T teleports along an aimed arc, B saves your position (unlimited slots, number keys jump
back), O runs a tour of every saved position, K saves a clean screenshot, V is a flashlight night mode,
J resets element graphics in the view, N is professional mode (hides the weapon permanently, everything
still works), M mutes. Esc pauses so Revit stays usable; Esc again exits.

Press Esc then S to remap any key - bindings are remembered between sessions.

On exit, any snags you marked are written to a report in Documents\AJ Game Snags. The only model change
Game Mode ever makes is creating the "AJ Game View" itself - camera movement creates no undo entries.

## AJ AI Assistant (chat + live model automation)

AJ AI is the AI-powered assistant built into AJ Tools - a chat panel that turns a plain-English
request into a C# script, then runs it against the open Revit document. It lives in two places:

- **C# pane** (AI Assistant panel, or the dockable AJ AI pane) - the interactive chat/code panel:
  type a request, click "Generate C# Code", review the script, click "Run Code".
- **AJ AI ribbon button** (AI Assistant panel) - a separate on/off toggle for the "AJ AI Bridge"
  (below): lets an external AI tool drive Revit the same way, without opening the chat panel.

### Settings (API keys and model)

Open Settings from the C# pane (gear icon) to choose an AI provider and enter its API key:

- **Gemini**, **OpenAI**, or **Claude** - pick one from the Provider dropdown. Each provider has
  its own API key field; OpenAI and Claude also let you pick a specific model (cheaper/faster vs.
  more capable) from a dropdown.
- API keys are encrypted on disk (Windows DPAPI, tied to the Windows user account) at
  `%AppData%\AJTools\AiShellConfig.json` - never stored in plain text, never sent anywhere except
  the chosen provider's own API.
- **Local Scripts Folder**: where "Save Script" writes generated `.cs` files, and where the Saved
  Scripts History list reads from. Any saved script can be "📌 Pin"ned to the ribbon for a
  one-click, no-code re-run (the "Run Pinned" button on the AI Assistant panel).

### Safety model

Every script - whether generated by the AI, typed in the pane's Live Console, or sent in through
the AJ AI Bridge - is scanned before it runs:

- **Blocked outright**: launching other programs, registry access, network calls, reflection
  tricks, deleting/moving files. These never run, no exceptions.
- **Needs your confirmation**: deleting or purging Revit elements, writing a file to disk. AJ AI
  shows a Yes/No popup describing exactly what the script will do before running it.
- Everything else runs immediately. This is a pattern-based safety net, not a full sandbox - review
  generated code before running it on a model you care about, the same as you would any script from
  any source.

### Connecting an external AI agent (AJ AI Bridge / MCP)

The **AJ AI** ribbon button (separate from the chat pane) starts a local bridge that lets an
external MCP-capable AI tool - such as Claude Code - run C# against the currently open Revit
document, with the same safety checks as above. This is how this repository itself is developed
and reviewed with AI help.

1. In Revit, click the **AJ AI** ribbon button (AI Assistant panel) to turn the bridge on - its
   icon changes to show it's connected.
2. In your MCP-capable AI tool, register this project's `mcp-server` folder as an MCP server
   (one-time setup - run `npm install` inside `mcp-server/` first). For Claude Code, that means an
   entry like this in `.mcp.json`:

   ```json
   {
     "mcpServers": {
       "aj-tools-aj-ai": {
         "command": "node",
         "args": ["<path-to-this-repo>/mcp-server/index.js"]
       }
     }
   }
   ```

3. Reconnect/restart your AI tool's MCP connection so it picks up the new server. It will then have
   three tools: `run_csharp` (run a snippet and get the result), `ping` (check the bridge is
   alive), and `model_summary` (fast read-only element counts for common MEP categories).

Notes:
- The bridge is local-machine-only (a named pipe, not a network port), and every request must
  carry a per-session token written to `%AppData%\AJTools\ajai-bridge.json` when you connect -
  nothing else on the machine can drive Revit through it unnoticed.
- Only one AI chat session can be "active" on the bridge at a time; opening a new one instantly
  takes over (matches finishing one chat and moving straight to the next).
- Every non-ping request is logged (timestamp, success, truncated code/output) to
  `%AppData%\AJTools\ajai-audit.jsonl` for your own review - a record of what ran, not a safety
  control by itself.
- Turn the bridge off with the same ribbon button when you're done - it also stops automatically
  when Revit closes.

## Typical Workflow
1. Open a Revit project (non-template view). Revit 2020 is the validated version; 2021–2027 builds exist but need Revit-side validation.
2. Use AJ Tools commands from the AJ Tools ribbon tab.
3. For model cleanup, start with View, Graphics, and Datums tools.
4. For annotation consistency, use Dimensions, Annotation, and Tags tools.
5. For MEP workflows, use Smart Connect, Ceiling Grid, HVAC Schematic, and Duct Flow tools as needed.

## Notes
- Toggle Links changes the Revit Links category visibility setting for the active view only.
- Unhide All clears Temporary Hide/Isolate and permanently hidden elements in the active view.
- View Crop supports plan, section, elevation, area plan, engineering plan, and detail/callout views.
- View Crop skips views controlled by scope boxes or view templates that lock crop settings.
- Purge Unplaced 3D Views and Purge Unplaced Sections preview non-template unplaced views separately, skip the active view and default `{3D}` view where applicable, and report purged, skipped, and failed counts.
- Apply Graphics uses one selected-element source for both modes, remembers the last-used settings, and applies either element overrides directly or category overrides from the categories found in those selected elements.
- Reset Category Graphics in View clears all overridable active-view categories, including annotation categories.
- Reset Element Graphics in View clears document element overrides in the active view.
- Duct Reference Dimension tools work in floor, ceiling, and engineering plan views.
- Active View Duct Dimensions skips vertical ducts, ducts shorter than 1000 mm, and ducts already covered by existing dimensions.
- HVAC Schematic creates a new drafting view from selected supported HVAC elements only.
- Ceiling Magnet snaps point-based elements after one ceiling-grid anchor pick.
- Auto Dims requires Crop View enabled and plan/section/elevation context.
- Some tools are blocked by view template locks.
- Very large models may take longer in Filter Pro value scanning.
- Smart Selection matches by category only (not family/type): pick one reference element, then one
  window or crossing box-select adds only elements sharing that category - it completes as soon as the
  box is drawn, no Finish/Enter step. Press Esc during the first pick to cancel; Esc during the
  follow-up box keeps just the reference element selected.
- Reassign Reference Level works two ways: Whole Project (pick a FROM level and a TO level) or Selected
  Elements (pre-select first, then pick only a TO level - each element's own level is read as its FROM,
  so a mixed-level selection is fine). The Selected Elements option stays disabled, with a tooltip
  explaining why, until something eligible is selected. Hosted elements are skipped either way.
- Purge Unplaced and Purge Unused are different questions. Unplaced means "not on a sheet" (views).
  Unused means "nothing references it" (view templates, filters, group types with no placed instances).
  Both probe before deleting - a rolled-back delete decides what Revit actually permits, rather than
  trusting a static scan, which catches cases like a template quietly set as Revit's own default.
- Transfer tools copy between two open projects. In override mode the copy's sheet placements are
  restored too - and a Legend placed on several sheets at once gets all of them back.
- Game Mode needs a 3D-capable model and creates one view the first time it runs. It is the only tool
  in the suite that is deliberately not about production output.
