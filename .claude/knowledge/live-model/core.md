# Live Model Notes — AJ AI Bridge scripting

> Entry point of the live-model knowledge set. Index: [`README.md`](README.md) — route from there to the topic you need.

Technical notes specific to writing C# snippets run via the `mcp__aj-tools-aj-ai__run_csharp` /
`ping` MCP tools against Ajmal's live, open Revit document. Separate concern from
[`ajtools-conventions.md`](../ajtools-conventions.md), which is about the *compiled AJ Tools plugin project*
— these are two different code contexts (ad-hoc script vs. real source file) and a gotcha in one doesn't
necessarily apply to the other.

**This file explains the recipes; [`.claude/scripts/`](../../scripts/README.md) holds the actual working
code.** Check the scripts folder before writing new C#, and when a recipe here changes, update its
script too so the two never drift apart. Two shapes live there: `filters/` + `actions/` — small,
element-type-agnostic fragments composed per request (e.g. "which elements" + "what to do to them", see
the scripts README for Ajmal's own worked example) — and `recipes/`, for the genuinely bespoke,
order-dependent, multi-stage builds below (HVAC placement/routing, MEP trace) that create new elements
rather than just act on existing ones and don't fit the filter+action shape.

**Contents** (this file is long — jump to the section you need, don't re-derive what's already here):
- Bridge basics — ping first, report version+model, script globals, what's blocked
- Revit version + unit conversion — 2020 `DisplayUnitType`, mm↔feet, fully-qualified types
- View visibility patterns — isolate/hide/reset, verify view state fresh each turn
- Tracing real MEP connectivity — bulk clustering, geometric trace, color-coding
- Undoing a mistake — native Revit Undo via PostCommand, never a delete script
- HVAC air terminal layout — Space airflow params, matched counts, checkerboard `(row+col)%2`,
  near-square row formula, grid orientation, Flow-parameter gotcha, multi-FCU zoning, `IsPointInRoom` Z
- Rotating equipment to face a target — connector identification (Fresh Air decoy), rotation math
- Drawing a duct between two points — sizing to the source connector, BreakCurve + explicit reconnect
- Branch duct from terminal to main duct — riser + real elbow + takeoff, cap-end recipe (7 steps)
- Slicing a main trunk for duct sizing — HIGH RISK, offset-cut recipe, orphaned-branch recovery
- Posting AJ Tools' own ribbon commands — doesn't work, don't re-attempt

## Bridge basics
- For a common category count with one optional parameter breakdown, prefer the native
  `model_summary` MCP tool when it is exposed. It performs one read-only bridge call and returns the
  Revit version and model title, so a separate ping is unnecessary. Keep `run_csharp` for complex,
  multi-parameter, geometry, and model-changing work.
- Always `mcp__aj-tools-aj-ai__ping` first if it's been a while — if it fails, Revit is closed or
  the AJ AI pane's Connect AJ AI Bridge toggle is off. Ask Ajmal to reconnect rather than guessing.
- **Whenever reporting a successful ping, always also report the session snapshot** — Ajmal wants this
  every time, not just on request (rule extended 2026-07-16: active view added to the original
  version+model rule). Get it in one follow-up `run_csharp` call by running
  [`scripts/context/context-active-view.cs`](../../scripts/context/context-active-view.cs), which returns
  everything the report needs: Revit version, model title (+ family vs project, worksharing), active view
  name/type, open views, and current selection count. Report compactly, e.g. "Connected — Revit 2020,
  model: MODEL PROJECT, active view: {3D} (3D), nothing selected." A bare "pong" with no snapshot is an
  incomplete ping report.
- Globals available directly in scripts: `Document`, `UIDocument`, `Application`, `UIApplication`. No
  `using AJTools...` — the script isn't compiled with a reference to AJTools.dll.
- Destructive ops (Delete/Purge/file writes) are refused unless `allowDestructive: true` is explicitly
  passed. This is deliberate — don't route around it.
- **Reflection / assembly-loading is hard-blocked** ("Loads assemblies or uses reflection to bypass normal
  API usage") — cannot reach into AJTools' own internal (non-public) classes this way. Only plain Revit
  API calls work. If a task seems to need this, do it with plain Revit API calls instead, or ask Ajmal to
  run the real tool himself.
- Multi-statement scripts need an explicit `return` — a trailing expression-without-semicolon (Roslyn
  scripting convention) does not reliably produce output here; the last line should be `return sb.ToString();`
  not just `sb.ToString();`.
- **Declaring a class in a script? Give its methods EXPRESSION bodies, not block bodies.** The bridge
  rewrites every block-bodied method to inject its own `__ajRecursionDepth` guard field, and in a class
  declared inside the script that injected code refers to the wrapper's instance field from a nested type —
  so it fails to compile with a confusing `CS0120: An object reference is required for the non-static field,
  method, or property '__ajRecursionDepth'`, pointing at lines of *your* method that look perfectly fine.
  Nothing is actually wrong with the code; only the body style matters. Confirmed 2026-08-11 implementing
  `IDuplicateTypeNamesHandler` for a document-to-document copy — identical class, block body failed,
  `=> DuplicateTypeAction.UseDestinationTypes;` compiled and ran. Applies to any interface a Revit API call
  needs you to implement inline (duplicate-type handlers, failure handlers, selection filters).
- **A bridge call can transiently fail with "Revit UI was blocked by another command/tool or window"**
  even with no user action in between — this is Revit being momentarily busy, not a real error. Simply
  retry the same call; it recovers on its own. Don't treat one blocked response as a reason to change
  approach or report a failure.
- **Discover a category's real parameter names/IDs before bulk reading or writing on it, don't guess from
  a plausible name.** Run [`../../scripts/actions/action-report-parameters.cs`](../../scripts/actions/action-report-parameters.cs)
  (or a one-off parameter dump) against one representative element of a category the first time it comes
  up in a session — parameter names vary by family/template, and a guessed name that happens to work on
  one project can silently miss or fail on another.
- **Watch for unbounded output on a large/complex query** — collecting or reporting every element in a
  big 3D view, or a whole-model dump with no category/region filter, can produce a very large response.
  Prefer a targeted filter (category, region, selection) over a blanket collector, and cap row counts on
  report actions (`maxRows` INPUTS already do this on the `report-*` fragments) rather than dumping
  everything.
- **Re-check the model/document identity if a session runs long.** Ajmal can close, switch, or open a
  different Revit document without saying so. If a later call's `context-active-view.cs` snapshot shows a
  different model title than earlier in the same conversation, treat every earlier element ID / view ID /
  family name from before the switch as invalid — re-orient before continuing, don't assume continuity.
- **An element hosted on a linked model's face (not this document's own levels) reports `LevelId ==
  InvalidElementId` — this is expected, not a bug.** Grouping such elements by level via the normal
  `LevelId`/level-parameter lookup silently fails for them. If level-grouping matters for an element like
  this, read its real Z coordinate (`get_Location`-style bounding-box or `LocationPoint.Point.Z`) and
  compare against known level elevations instead.

## Revit version + unit conversion
- This project's live Revit session has been **2020** in testing so far — use
  `UnitUtils.ConvertToInternalUnits(mm, DisplayUnitType.DUT_MILLIMETERS)`, not `UnitTypeId.Millimeters`
  (that enum is 2021+ only). If a future session shows a different open version, check before assuming.
- Ajmal always speaks in **mm**, Revit's internal API is always **feet** — convert both ways explicitly,
  don't leave raw feet in a reply.
- `Autodesk.Revit.DB.Structure.StructuralType` must be **fully qualified** when calling
  `Document.Create.NewFamilyInstance(...)` — a bare `StructuralType` fails to compile in this script
  context ("inaccessible due to its protection level").
- Same fully-qualify rule hits MEP types in this script context: `Autodesk.Revit.DB.Mechanical.Duct` and
  `Autodesk.Revit.DB.Mechanical.MechanicalSystemType` — a bare `Duct`/`MechanicalSystemType` fails with
  "type or namespace not found". `Connector.DuctSystemType`'s enum type (`Autodesk.Revit.DB.Mechanical.
  DuctSystemType`) goes further — even fully qualified it's "inaccessible due to its protection level" (the
  enum itself isn't public in this script context, only the property that returns it is) — compare via
  `connector.DuctSystemType.ToString() == "SupplyAir"` instead of referencing the enum type directly.
- `new ElementId(someLong)` fails to compile with a confusing error — "cannot convert from 'long' to
  'Autodesk.Revit.DB.BuiltInParameter'" — because this Revit version's `ElementId` only has an `(int)` and
  a legacy `(BuiltInParameter)` constructor, no `(long)` overload; `long` doesn't implicitly narrow to
  `int` so the compiler falls through to the wrong overload. Cast explicitly: `new ElementId((int)someLong)`.

### Category ID quick reference (for reading raw output only — never hardcode these in scripts)
Verified live (2026-07-14) against the real installed RevitAPI.dll — all 27 matched exactly, none wrong:

| Category | Id | Category | Id |
|---|---|---|---|
| Walls | -2000011 | Sheets | -2003100 |
| Doors | -2000023 | Schedules | -2000573 |
| Windows | -2000014 | Levels | -2000240 |
| Floors | -2000032 | Grids | -2000220 |
| Roofs | -2000035 | Views | -2000279 |
| Ceilings | -2000038 | Viewports | -2000510 |
| Rooms | -2000160 | MEP Spaces | -2003600 |
| Stairs | -2000120 | Plumbing Fixtures | -2001160 |
| Columns | -2000100 | Lighting Fixtures | -2001120 |
| Structural Framing | -2001320 | Mechanical Equipment | -2001140 |
| Curtain Wall Panels | -2000170 | Electrical Equipment | -2001040 |
| Curtain Wall Mullions | -2000171 | Generic Model | -2000151 |
| Furniture | -2000080 | Casework | -2001000 |
| Planting | -2001360 | | |

**Why this is a reference, not something scripts should use directly**: every fragment in `.claude/scripts/`
writes the symbolic name (`BuiltInCategory.OST_Walls`), never the raw negative number — a typo in the enum
name is a compile error, a typo in a raw int (e.g. transposing `-2001320` and `-2001360`) would silently
point at the wrong category with no warning. This table is only useful for recognizing a bare category Id
when it shows up in raw output (a warning, an export, a debug dump) — converting int→enum in a script is
always a live one-line cast (`(BuiltInCategory)someInt`) or `Category.GetCategory(doc, id).Name`, which is
authoritative for every category, not just these 27 common ones.

