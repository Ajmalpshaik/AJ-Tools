# AJ Tools — Reusable AJ AI Bridge Scripts (index — start here)

This folder holds **working C# fragments**, not just descriptions of them, for jobs that come up
repeatedly on the live model via the AJ AI Bridge (`mcp__aj-tools-aj-ai__run_csharp`).
The point: the next session runs code that already worked, instead of re-deriving it from prose and
risking a small mistake creeping back in.

They're composed per request rather than rewritten each time. **Most requests split into "which elements"
(a `filters/` fragment — or `creators/` if they don't exist yet) + "what to do to them" (one or more
`actions/`).** Genuinely bespoke, order-dependent multi-stage builds live in `recipes/` instead.

**Read this file, pick the fragment, open that one file — don't read the whole folder.** Background lives
beside this index, not in it:

- **Why the folder is shaped this way** (the filter+action idea, Ajmal's worked example, the AJ Adaptive
  AI-Local Workflow, how the library grows) → [`architecture.md`](architecture.md)

Everything below is what an actual script task needs: the routing table, the rules, and the checkpoints.

## Current fragments

### Filters (produce `elements`)
| Fragment | Job |
|---|---|
| [`filter-by-category.cs`](filters/filter-by-category.cs) | Every instance of one category, optional level scope |
| [`filter-by-category-and-family.cs`](filters/filter-by-category-and-family.cs) | Category narrowed to a family name (VCD-style) |
| [`filter-by-category-and-numeric-param.cs`](filters/filter-by-category-and-numeric-param.cs) | Category narrowed by a numeric parameter vs. an mm value (the "500mm duct" filter) |
| [`filter-by-room.cs`](filters/filter-by-room.cs) | Category narrowed to instances physically inside one room |
| [`filter-by-system-type.cs`](filters/filter-by-system-type.cs) | Pipes/ducts/fittings narrowed by MEP system name |
| [`filter-by-current-selection.cs`](filters/filter-by-current-selection.cs) | Whatever's currently selected in Revit |
| [`filter-by-category-name.cs`](filters/filter-by-category-name.cs) | Category resolved by plain display name, not the BuiltInCategory enum |
| [`filter-by-region.cs`](filters/filter-by-region.cs) | Category narrowed to instances whose bounding box intersects a given mm region |
| [`filter-by-multiple-categories.cs`](filters/filter-by-multiple-categories.cs) | Several categories collected as one group, e.g. duct system / pipe system / cable tray system |
| [`filter-by-parameter-text.cs`](filters/filter-by-parameter-text.cs) | Category or whole-model scan narrowed by text in family/type/parameter values |
| [`filter-by-workset.cs`](filters/filter-by-workset.cs) | Elements on one user workset, optional category scope |
| [`filter-by-sheets.cs`](filters/filter-by-sheets.cs) | Every ViewSheet, optional sheet-number substring |
| [`filter-by-phase.cs`](filters/filter-by-phase.cs) | Elements matching a named Phase Created and/or Phase Demolished, optional category scope |
| [`filter-by-id-list.cs`](filters/filter-by-id-list.cs) | A specific list of Element Ids Ajmal already has — "what is this element / what are its parameters" |

### Actions (consume `elements`)
| Fragment | Job |
|---|---|
| [`action-set-color-uniform.cs`](actions/action-set-color-uniform.cs) | One color (line + solid fill) on every element |
| [`action-color-by-group.cs`](actions/action-color-by-group.cs) | Distinct color per group, grouped by any parameter's actual value; palette/gradient/random modes |
| [`action-highlight-vs-rest.cs`](actions/action-highlight-vs-rest.cs) | Highlight `elements` in one color, gray out every OTHER element in the active view |
| [`action-reset-graphic-overrides.cs`](actions/action-reset-graphic-overrides.cs) | Clear color/fill overrides |
| [`action-isolate-elements.cs`](actions/action-isolate-elements.cs) | Temporary isolate, reset-then-apply |
| [`action-hide-elements.cs`](actions/action-hide-elements.cs) | Hide (temporary by default, or permanent) |
| [`action-unhide-elements.cs`](actions/action-unhide-elements.cs) | Reverse a permanent hide |
| [`action-select-elements.cs`](actions/action-select-elements.cs) | Set as the active Revit selection |
| [`action-count-and-report.cs`](actions/action-count-and-report.cs) | Bare count or size-breakdown table |
| [`action-set-parameter-value.cs`](actions/action-set-parameter-value.cs) | Bulk-set one parameter across the set |
| [`action-set-transparency.cs`](actions/action-set-transparency.cs) | Set surface transparency (0-100%) |
| [`action-section-box-and-zoom.cs`](actions/action-section-box-and-zoom.cs) | Section-box a 3D view around `elements` and zoom to them |
| [`action-material-takeoff.cs`](actions/action-material-takeoff.cs) | Material area/volume quantities across `elements`, grouped by material |
| [`action-length-by-size.cs`](actions/action-length-by-size.cs) | Count + total length per size group, for linear MEP elements (duct/pipe/cable tray) |
| [`action-set-pin-state.cs`](actions/action-set-pin-state.cs) | Pin or unpin the filtered element set |
| [`action-report-parameters.cs`](actions/action-report-parameters.cs) | Parameter table for the filtered element set |
| [`action-show-elements.cs`](actions/action-show-elements.cs) | Zoom/show the filtered elements, optionally selecting them |
| [`action-extract-dates-from-textnotes.cs`](actions/action-extract-dates-from-textnotes.cs) | Scan every TextNote on each sheet for date-like text, report distinct dates + source sheet(s), read-only |
| [`action-assign-revisions-by-sheet-date.cs`](actions/action-assign-revisions-by-sheet-date.cs) | Attach each sheet's matching project Revision(s) via `SetAdditionalRevisionIds`, matched by date found in that sheet's TextNotes — writes the model, see gotcha note in `../knowledge/live-model/revisions.md` |
| [`action-copy-parameter-value.cs`](actions/action-copy-parameter-value.cs) | Copy one parameter's value into a different parameter, storage-type-aware |
| [`action-renumber-sequential.cs`](actions/action-renumber-sequential.cs) | Assign a sequential value (prefix/number/padding/suffix) to a String parameter, sorted by position or existing value |
| [`action-find-duplicates.cs`](actions/action-find-duplicates.cs) | QA check — flag elements whose insertion points sit within a tolerance of each other; read-only, optional select |
| [`action-move-elements.cs`](actions/action-move-elements.cs) | Translate every element by one mm offset vector |
| [`action-copy-elements.cs`](actions/action-copy-elements.cs) | Duplicate every element, offset by one mm vector; produces `newElementIds` for chaining |
| [`action-rotate-elements.cs`](actions/action-rotate-elements.cs) | Rotate every element around a vertical axis by one angle, about its own location or a given pivot |
| [`action-report-graphic-overrides.cs`](actions/action-report-graphic-overrides.cs) | Read back current view-specific graphic overrides per element — line color, fill color, transparency, halftone; read-only |
| [`action-place-viewport-on-sheet.cs`](actions/action-place-viewport-on-sheet.cs) | Place each view in the set onto one sheet as a Viewport (views can only sit on one sheet at a time) |
| [`action-place-schedule-on-sheet.cs`](actions/action-place-schedule-on-sheet.cs) | Place each schedule onto one sheet — same schedule can be placed on multiple sheets, no duplication needed |
| [`action-duplicate-views.cs`](actions/action-duplicate-views.cs) | Duplicate/duplicate-with-detailing/dependent-view each view in the set; produces `newViewIds` |
| [`action-set-view-crop.cs`](actions/action-set-view-crop.cs) | Crop the active view to fit the filtered element set + margin |
| [`action-report-location.cs`](actions/action-report-location.cs) | Report each element's position (point, line endpoints, or bounding-box-center fallback); read-only |
| [`action-report-bounding-box.cs`](actions/action-report-bounding-box.cs) | Report each element's bounding box + the combined extents of the set; read-only |
| [`action-change-element-type.cs`](actions/action-change-element-type.cs) | Bulk-swap every element's type to a different named type within the same family |
| [`action-delete-elements.cs`](actions/action-delete-elements.cs) | Permanently delete every element in the set — highest-risk fragment, explorer-first is mandatory, needs `allowDestructive: true` on the bridge call too |
| [`action-rename-element.cs`](actions/action-rename-element.cs) | Rename each element via `Element.Name` (views, sheets, levels, types — not most instance geometry); not yet live-verified |

### Creators (produce `elements` by creating new ones)
| Fragment | Job |
|---|---|
| [`create-levels.cs`](creators/create-levels.cs) | Batch-create levels, evenly spaced or at explicit elevations |
| [`create-material.cs`](creators/create-material.cs) | Create one or more Materials with a set colour and transparency |
| [`create-point-based-element.cs`](creators/create-point-based-element.cs) | Place a family instance at one or more points on a level |
| [`create-room.cs`](creators/create-room.cs) | Place a Room at one or more points on a level |
| [`create-sheet.cs`](creators/create-sheet.cs) | Create one or more new sheets with a chosen title block |
| [`create-schedule.cs`](creators/create-schedule.cs) | Create a bare schedule for a category with chosen fields — chain into `action-place-schedule-on-sheet.cs`; not yet live-verified |

### Recipes (bespoke multi-stage builds, not filter+action shaped)
| Recipe | Job | Source |
|---|---|---|
| [`recipes/trace-mep-circuits.cs`](recipes/trace-mep-circuits.cs) | Bulk-cluster a filtered pipe/duct system into physical circuits and find real endpoints | `../knowledge/live-model/mep-trace.md` § Tracing real MEP connectivity |
| [`recipes/set-space-airflow.cs`](recipes/set-space-airflow.cs) | Create/find each room's MEP Space, set Supply/Return Airflow, cascade to existing terminals | `../knowledge/live-model/hvac-terminals.md` § HVAC air terminal layout |
| [`recipes/place-terminals-checkerboard.cs`](recipes/place-terminals-checkerboard.cs) | Place a room's supply/return terminals in a near-square checkerboard grid | `../knowledge/live-model/hvac-terminals.md` § HVAC air terminal layout |
| [`recipes/place-fcu.cs`](recipes/place-fcu.cs) | Place an FCU, reposition toward the door, rotate to face terminals | `../knowledge/live-model/hvac-ducts.md` § Placing equipment relative to a door |
| [`recipes/draw-main-duct-with-cap.cs`](recipes/draw-main-duct-with-cap.cs) | Draw a sized main duct from the FCU and cap every open end correctly | `../knowledge/live-model/hvac-ducts.md` § Drawing a duct, § cap-end recipe |
| [`recipes/connect-terminal-branch.cs`](recipes/connect-terminal-branch.cs) | Riser + real elbow + takeoff tee connecting a terminal to the main duct | `../knowledge/live-model/hvac-ducts.md` § Branch duct from a terminal |
| [`recipes/verify-duct-connectivity.cs`](recipes/verify-duct-connectivity.cs) | Trace every terminal's full connector chain to its FCU | `../knowledge/live-model/hvac-ducts.md` (orphan-recovery trace) |
| [`recipes/slice-trunk-for-sizing.cs`](recipes/slice-trunk-for-sizing.cs) | HIGH RISK — slice a main trunk at each takeoff (grouped, checkerboard-aware), offset past the fitting body, for later per-segment sizing | `../knowledge/live-model/hvac-ducts.md` § Slicing a main trunk into segments for duct sizing |
| [`recipes/split-duct-near-equipment.cs`](recipes/split-duct-near-equipment.cs) | Split a duct at a fixed gap from an equipment connector (e.g. a future flex-duct gap at an FCU) and reconnect the joint — NOT a standing default, only on explicit request | `../knowledge/live-model/hvac-ducts.md` § Splitting an existing duct into two segments at a given point |
| [`recipes/create-revisions-from-sheet-dates.cs`](recipes/create-revisions-from-sheet-dates.cs) | Scan sheet TextNotes for dates, create one project-level Revision per distinct date, oldest first | `ajtools-conventions.md` (Revision API) |
| [`recipes/tag-elements-in-active-view.cs`](recipes/tag-elements-in-active-view.cs) | Tag every element of one category in the active view with a working L-shaped leader — direct live-model alternative to clicking Smart MEP Tags; simplified placement, not full clash-scoring | `../knowledge/live-model/tagging.md` § AJTools internal classes unreachable from scripts |
| [`recipes/ray-trace-to-ceiling.cs`](recipes/ray-trace-to-ceiling.cs) | Ray-cast straight up from each element to the nearest ceiling above it and snap the element's height to the hit point | Ajmal's own idea (2026-07-14); positive case not yet live-verified — no Ceiling exists in this model yet |
| [`recipes/create-parametric-box-family-with-duct-connector.cs`](recipes/create-parametric-box-family-with-duct-connector.cs) | Family Editor authoring (not project-doc editing): set category, build a parametric box body extrusion + optional rectangular neck stub + duct connector, all resizable via Length/Width/Height/Neck Width/Neck Height/Neck Depth parameters | `../knowledge/live-model/families.md` § Building a parametric family from scratch |

### Commands (no element set)
| Command | Job |
|---|---|
| [`commands/native-undo.cs`](commands/native-undo.cs) | Revert the last transaction via Revit's own Undo |
| [`commands/unhide-all-active-view.cs`](commands/unhide-all-active-view.cs) | Restore permanently hidden elements and clear Temporary Hide/Isolate in the active view |

### Context (whole-document, read-only orientation — no element set, model never changes)
| Fragment | Job |
|---|---|
| [`context/context-active-view.cs`](context/context-active-view.cs) | Session snapshot — Revit version, active model (family/project, worksharing, open docs) + active view name/type/scale/level, screen Right/Up directions, open views, selection count. Standing follow-up to every successful ping (core.md rule) |
| [`context/context-project-units.cs`](context/context-project-units.cs) | Every unit spec valid for this document and its current display unit (mm/m, CFM/L/s, etc.) |
| [`context/context-all-warnings.cs`](context/context-all-warnings.cs) | Every model warning — severity, description, failing element Ids; optional Error-only filter |
| [`context/context-workset-info.cs`](context/context-workset-info.cs) | Worksharing on/off, and every user workset with open/closed state and owner |
| [`context/context-model-categories.cs`](context/context-model-categories.cs) | Model categories, keyword-filterable (avoid an unfiltered full-model dump) |
| [`context/context-used-families.cs`](context/context-used-families.cs) | Every loadable family in the model, excluding system and in-place families |

"Current selection" is already covered by [`filters/filter-by-current-selection.cs`](filters/filter-by-current-selection.cs) — not duplicated here.

### Examples (fully assembled)
| Example | Demonstrates |
|---|---|
| [`examples/color-isolate-select-by-size.cs`](examples/color-isolate-select-by-size.cs) | filter-by-category-and-numeric-param + 3 chained actions, Ajmal's own worked scenario |


## The rules that apply to every script

## Always report the Element ID for specific elements

Any time output names/reports on **specific elements** (not a bare count) — a report table, a "here's
what I found/changed" list, a list of elements needing a decision — include each one's **Element ID** in
the output. It's the one identifier guaranteed unique per element in a model (see the "Element ID" entry
in [`../knowledge/glossary.md`](../knowledge/glossary.md)), so it's what lets Ajmal re-select, verify, or
reference that exact element later (including via
[`filters/filter-by-id-list.cs`](filters/filter-by-id-list.cs)). The `action-report-*` fragments already
do this by default — keep that default on when writing a new one, and don't drop it just to shorten
output.

## Modular-by-default rule

A direct one-off snippet is fine for a quick live test, but if the idea is worth saving in
`.claude/scripts/`, convert it into reusable modules instead of saving the one-off shape.


## Explorer first, invoker second — for anything bulk or hard to reverse

For a request that's large in scope or not cheaply undone, **run the filter fragment alone first**
(paste just the filter, add your own `return sb.ToString();`, run it) to see the real count before
appending any action. Confirm that count matches what Ajmal expects, *then* re-run the full composed
script with the action(s) attached. This is the same "confirm before bulk" rule already in `CLAUDE.md`
and every HVAC skill — the filter/action split just makes the two steps literally separable instead of
having to mentally simulate the filter's result before running a monolithic script.

For a small, cheap, easily-undone request, just run the composed script directly — don't add ceremony
that doesn't earn its cost.

## Transaction safety — explicit rollback, never a silent throw

Every action fragment (and every `recipes/` script) wraps its `Transaction` in a try/catch that calls
`.RollBack()` and appends a clear reason to `sb` on failure, instead of letting an exception propagate
as a bare, uninformative error through the bridge. For a `recipes/` script with multiple dependent
transactions (draw a duct, then cap it), the whole sequence runs inside one `TransactionGroup` —
`group.Assimilate()` only on full success, `group.RollBack()` on any failure — so a mid-sequence error
can never leave a half-built result behind (e.g. a duct drawn but never capped). This came directly out
of a real incident: `draw-main-duct-with-cap.cs` once left an inconsistent model state after a partial
failure, which is exactly the failure mode a `TransactionGroup` prevents. Apply this same pattern
whenever a `recipes/` script is next touched, even if it hasn't been updated yet.

## Every number is a per-request input — never a default

Same rule as everywhere else in this project. Every fragment's `INPUTS` block (size in mm, color,
parameter name, room id, comparison operator) came from a specific request. Restate/confirm the current
one before running — the pre-filled values exist so there's one obvious place to edit, not because
they're safe to reuse blindly.


## How to compose two or more fragments into one script

1. Pick the filter fragment that matches the request; open it and read its `INPUTS` block.
2. Pick one or more action fragments; read each one's `INPUTS` block too.
3. Paste the filter fragment's body first, then each action fragment's body in the order they should
   run, into one script. Every fragment shares the same two variable names — `elements` and `sb` — so
   they chain without any glue code. None of them end in `return`; you add exactly one
   `return sb.ToString();` as the very last line of the whole composed script.
4. Fill in every `INPUTS` block with today's actual values — nothing pre-filled in these files is a
   default, per the rule below.
5. Run the composed script via `mcp__aj-tools-aj-ai__run_csharp`.

If the native MCP tool is not exposed in the current agent session, do not spend time re-reading
`mcp-server/index.js` or hand-writing a named-pipe wrapper. Use the checked-in fallback helper:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\invoke-revit-bridge.ps1 -Ping
powershell -NoProfile -ExecutionPolicy Bypass -File tools\invoke-revit-bridge.ps1 -CodeFile <composed-script.cs>
powershell -NoProfile -ExecutionPolicy Bypass -File ..\tools\invoke-aj-ai-bridge.ps1 -Ping
powershell -NoProfile -ExecutionPolicy Bypass -File ..\tools\invoke-aj-ai-bridge.ps1 -CodeFile <composed-script.cs>
```

`tools\invoke-revit-bridge.ps1` is a visible root shortcut for agents that search with plain `rg --files`
and therefore miss dot folders like `.claude`. These helpers exist only as fallbacks. If
`mcp__aj-tools-aj-ai__run_csharp` is available, use the native MCP tool directly.


## Before writing new AJ AI Bridge C#

Check `filters/` and `actions/` first — compose from what's there rather than writing a filter or an
action from scratch. Only write a new fragment if nothing existing covers the job; only write a
one-off, non-fragment script if it's genuinely not going to repeat (and even then, consider whether it's
actually a `recipe` in disguise).

## After running something new

If what you wrote (or composed) used a new *kind* of filter or action not covered here, save it as its
own fragment — or update the closest existing one — following the naming pattern
(`filter-by-<what>.cs`, `action-<verb>-<what>.cs`). If it was a true one-off, don't save it. Always
verify a fragment's result against the real model with a fresh read-back after running it (Modeler
mindset in `CLAUDE.md` applies here too).


## After adding, updating, or retiring a fragment

Add one short dated line to `ajtools-conventions.md`'s Log — same as any other AJ Tools decision. If a
fragment is retired because the job it did doesn't come up anymore, say so and delete it rather than
leaving a stale file that looks current.
