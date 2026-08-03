# AJ Tools — Scripts: the architecture behind the folder

> Background for [`README.md`](README.md) — the index you actually route from. Read this once to
> understand *why* fragments are shaped this way; you don't need it to run a normal script task.

## The architecture: filter + action, composed per request (Ajmal's idea, 2026-07-09)

Most live-model requests are really two separate concerns glued together: **which elements** (a
category, a size, a room, a system type...) and **what to do to them** (color, isolate, select, count,
set a parameter). Writing one monolithic script per *combination* means rewriting the filtering logic
every single time it's paired with a different action, and rewriting the action every time it's paired
with a different filter. So instead:

- **[`filters/`](filters/)** — each fragment produces a `List<Element> elements` given some INPUTS
  (category, family, size, room, system type, or "whatever's currently selected"). One filter is
  written once and reused by every action.
- **[`actions/`](actions/)** — each fragment *consumes* `elements` (already filtered by whichever
  fragment ran before it) and does exactly one thing: color, isolate, hide, select, count/report, or
  set a parameter. One action is written once and reused by every filter.
- **[`creators/`](creators/)** — fills the same role as `filters/`: produces `elements`, but by
  *creating* new elements (levels, point-placed instances, rooms) instead of querying existing ones.
  Chains into `actions/` exactly like a filter does — "create 3 levels, then rename/report on them" is
  just a creator followed by an action, same composition contract.
- **[`recipes/`](recipes/)** — the genuinely bespoke, multi-stage, order-dependent workflows that
  *create new elements* with real ordering/geometry dependencies between steps (the HVAC placement/
  routing chain). These don't fit the filter→action shape even with `creators/` — they stay as their
  own dedicated scripts.
- **[`commands/`](commands/)** — whole-document commands with no element set at all (currently just
  Undo).
- **[`context/`](context/)** — whole-document, **read-only** orientation queries with no element set and
  usually no input at all (active view, project units, warnings, worksets, model categories, loaded
  families). Same shape as `commands/`, but never modifies the model — safe to run anytime, including
  mid-task, just to check where things stand.
- **[`examples/`](examples/)** — fully assembled, ready-to-paste compositions, so the pattern below is
  concrete, not just described.

### Ajmal's own example, worked through

*"Change the color of the 500mm-height ducts, then isolate and select them."*

```
filters/filter-by-category-and-numeric-param.cs   (Ducts, Height, = 500mm)  → produces `elements`
    + actions/action-set-color-uniform.cs                                   → colors `elements`
    + actions/action-isolate-elements.cs                                    → isolates `elements`
    + actions/action-select-elements.cs                                     → selects `elements`
```

The fully assembled result is [`examples/color-isolate-select-by-size.cs`](examples/color-isolate-select-by-size.cs)
— copy that shape for any new combination: same category filtered a different way, or the same 500mm
duct filter paired with a totally different action (e.g. `action-count-and-report.cs` instead, for
"how many 500mm ducts are there").

**If the element type changes** (pipes instead of ducts, a family instead of a raw category), swap the
filter fragment only — every action fragment already works on `elements` regardless of what kind of
element is in it. That's the whole benefit of the split: one filter fragment change, not one script
rewritten from scratch per element type.


## AJ Adaptive AI-Local Workflow

This folder is the local half of Ajmal's AI workflow. The aim is not for AI to rewrite C# from zero
every time. The AI/local split is flexible, not a fixed percentage:

- **AI side**: understand Ajmal's dictated request, map the words to Revit categories/parameters, choose
  the right filter/action/recipe, fill the per-request inputs, and decide what needs verification.
- **Local side**: reuse tested `.claude/scripts/` fragments, `.claude/knowledge/` facts, and the
  AJ AI Bridge to run against the real model quickly and consistently.

For common tasks the local side should do most of the work. For new/unclear tasks the AI side may do most
of the reasoning first, then save the reusable part afterward if it should repeat.

The loop is:

```text
request -> route by shape -> compose local modules -> run -> verify -> answer -> improve the library
```

Decision order:

```text
1. Reuse an existing local module if one fits.
2. If nothing fits, do the task normally with the smallest correct AI-written script.
3. After verification, check whether that new script/pattern should become reusable.
```

First time a task appears, a one-off script may be acceptable. If the shape repeats, convert it into a
filter/action/creator/recipe so the next run is faster. Day by day, the AI should write less fresh code
and assemble more from the local library.

If no local module fits yet, AI still owns the task:

```text
no matching module -> AI writes the smallest correct one-off -> run -> verify -> answer
```

Then decide immediately whether the shape is reusable:

- If it is likely to repeat, save the reusable part as a new filter/action/creator/recipe and update this
  README.
- If it is truly one-time, do not save it; just report the result and any lesson learned.
- If it revealed an API/model gotcha, record that in `../knowledge/live-model/ (the right topic file)` even if the code
  itself is not saved.

Example one-off:

```csharp
var pipes = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_PipeCurves)
    .WhereElementIsNotElementType()
    .ToElements();

return "Total pipes: " + pipes.Count;
```

Saved/reused version:

```text
filters/filter-by-category.cs      (targetCategory = OST_PipeCurves) -> produces elements
    + actions/action-count-and-report.cs                            -> reports Count
```

So "count pipes", "count ducts", "select pipes", "color pipes", and "isolate pipes" do not become five
separate scripts. They reuse the same category filter and swap only the action.

**Do not search this folder by element nouns first** (`*duct*`, `*pipe*`, `*VCD*`, etc.) to decide
whether a reusable script exists. Most reusable fragments are intentionally generic and are named by
their job shape (`filter-by-category`, `action-count-and-report`, `action-isolate-elements`), not by the
Revit category they will be used on today. Route the request by shape first:

| Request shape | Compose |
|---|---|
| "How many X?" | `filters/filter-by-category.cs` + `actions/action-count-and-report.cs` |
| "How many X, what height/diameter/size?" | `filters/filter-by-category.cs` + `actions/action-count-and-report.cs` with `wantBreakdownTable = true`, `preferredParamName` set |
| "How many X, with size AND total length per size?" | `filters/filter-by-category.cs` + `actions/action-length-by-size.cs` |
| "Show me/list the 300x300 X" (a specific value, not a full breakdown) | matching filter (e.g. `filter-by-category-and-numeric-param.cs`) + `actions/action-report-parameters.cs` — lists the actual items with Element ID, not just a count |
| "Select/color/isolate/hide X" | `filters/filter-by-category.cs` + the matching action |
| "X inside this room" | `filters/filter-by-room.cs` + the matching action |
| "X with this family/name/parameter" | `filter-by-category-and-family.cs` or `filter-by-category-and-numeric-param.cs` + the matching action |
| "All duct system / pipe system / cable tray system" | `filters/filter-by-multiple-categories.cs` + the matching action |
| "X where parameter/family/type contains Y" | `filters/filter-by-parameter-text.cs` + the matching action |
| "X on workset Y" | `filters/filter-by-workset.cs` + the matching action |
| "Pin/unpin X" | any filter + `actions/action-set-pin-state.cs` |
| "Show/zoom to X" | any filter + `actions/action-show-elements.cs` |
| "Report these parameters for X" | any filter + `actions/action-report-parameters.cs` |
| "Unhide all in this view" | `commands/unhide-all-active-view.cs` |
| "What dates are written on the sheets / title blocks?" | `filters/filter-by-sheets.cs` + `actions/action-extract-dates-from-textnotes.cs` |
| "Attach/set the matching revision onto each sheet" | `filters/filter-by-sheets.cs` + `actions/action-assign-revisions-by-sheet-date.cs` |

Typical live request:

> "Ping the model and tell me how many ducts are there, and what height they are."

Do this as workflow + modules, not as a new saved script:

```text
1. mcp__aj-tools-aj-ai__ping
2. report Revit version + open model, per ../knowledge/live-model/core.md
3. filters/filter-by-category.cs
   targetCategory = BuiltInCategory.OST_DuctCurves
4. actions/action-count-and-report.cs
   wantBreakdownTable = true
   preferredParamName = "Height"
5. return sb.ToString()
```

If Ajmal asks the same question for pipes, swap only the filter/category and preferred parameter
(`OST_PipeCurves`, `"Diameter"`). If he asks to select/color/isolate those ducts after counting them,
keep the same duct filter and append a different action.


## This is a living folder, not a one-time drop

Same convention as `.claude/skills/`: nothing here is final.

- **Update a fragment in place** when it turns out wrong or a better pattern is found — don't fork
  `-v2.cs` next to the old one.
- **Add a new filter or action** the first time a genuinely reusable one proves itself — not for a
  one-off that will never repeat. Check whether an existing fragment already covers it (a slightly
  different parameter name, a slightly different category) before adding a near-duplicate.
- **Refactor freely** — split, merge, or rename fragments as the underlying jobs turn out to be the
  same or different in practice.
- Every script here returns an explicit report string (`return sb.ToString();` on the final line of
  the composed script) per the Roslyn-scripting gotcha in `../knowledge/live-model/core.md` — a trailing bare
  expression doesn't reliably produce output through the bridge.

