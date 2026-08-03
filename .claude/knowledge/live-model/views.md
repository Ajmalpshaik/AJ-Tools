# Live Model — View visibility & section views

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## View visibility patterns (used a lot — isolate/hide requests are common)
- **Isolate by category**: `view.IsolateCategoriesTemporary(List<ElementId> categoryIds)` inside a
  `Transaction`. Only works on `View3D`/plan/section-type views, not schedules — check
  `view is View3D` (or the relevant type) first and bail out clearly if it's the wrong kind of view.
- **Isolate specific elements** (not a whole category): `view.IsolateElementsTemporary(List<ElementId> ids)`
  — use when Ajmal wants to see only a subset of a category (e.g. one size of VCD), not the whole category.
- **Reset before re-isolating to a *different* subset**, so the new isolation fully replaces the old one
  rather than assuming it's additive: `view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate)`
  then apply the new isolate call. In practice a fresh `IsolateElementsTemporary`/`IsolateCategoriesTemporary`
  call already replaces the prior state on its own, but reset-then-apply is the clearest way to reason
  about it when Ajmal describes it as "unhide X, then show only Y."
- These are **temporary** (dashed cyan border in Revit), not permanent visibility overrides — always tell
  Ajmal that "Reset Temporary Hide/Isolate" clears it, in case he wants it made permanent instead
  (`view.SetCategoryHidden(catId, true)` per category, inside a transaction, if he ever asks for that).

**Don't assume view state from an earlier turn is still there — verify, don't recall.** Isolation and
per-element color overrides (`SetElementOverrides`) can be cleared between messages — by Ajmal resetting
Temporary Hide/Isolate himself, using Revit's Undo, or editing Visibility/Graphics directly. Confirmed
2026-07-08: color overrides applied in one turn were gone by a later turn with no error or warning — a
`view.GetElementOverrides(id).ProjectionLineColor.IsValid` check caught it before wrongly reporting
"already colored, nothing to do." Cheap insurance: for anything you set on the view in a previous turn
(isolation, colors, hidden categories) that a later request depends on, do a quick read-back check before
assuming it survived, rather than trusting your own earlier tool-call result as still-current truth.

**Reading overrides back: the pattern-visibility getters lie by default, colors don't.** Confirmed
2026-07-14 building `action-report-graphic-overrides.cs`: `OverrideGraphicSettings.IsSurfaceForegroundPatternVisible`
/ `IsCutForegroundPatternVisible` return `true` even on a completely untouched element — they mean
"would a pattern show if one were set," not "an override exists." Using them as the "has an override"
signal produced false positives on 5 real, genuinely clean duct terminals. The real signal is color/pattern
validity — `ProjectionLineColor.IsValid`, `CutLineColor.IsValid`, `SurfaceForegroundPatternColor.IsValid`,
`CutForegroundPatternColor.IsValid`, or a real (non-`InvalidElementId`) pattern Id — same check already
proven above for staleness detection. Also confirmed via reflection, not memory: the getters are named
`IsSurfaceForegroundPatternVisible`/`IsCutForegroundPatternVisible` (bool properties), not
`SurfaceForegroundPatternVisible` — guessing the un-prefixed name is a compile error, not a silent bug.

**Current selection can include elements not present in the active view's collector.** Confirmed
2026-07-19 running `action-highlight-vs-rest.cs` (color selection red, rest of active 3D view gray): 22
elements were selected but only 19 showed up in `new FilteredElementCollector(doc, view.Id)` — the other
3 were `IndependentTag`s (Equipment Tags) that belonged to a different open plan view, not the active 3D
view. Revit's selection (`UIDocument.Selection.GetElementIds()`) is document-wide, not scoped to the
active view, and tags in particular don't exist/display in 3D views at all. Don't treat a mismatch
between "selected count" and "highlighted count" as a bug — cross-check which of the selected ids are
actually `inViewCollector` before reporting a discrepancy as an error.

## Creating a section view (`ViewSection.CreateSection`) — the Transform must be right-handed
- `ViewSection.CreateSection(doc, viewFamilyTypeId, sectionBox)` takes a `BoundingBoxXYZ` whose
  `Transform` needs `BasisZ` to equal `BasisX cross BasisY` (a proper right-handed orthonormal basis).
  Get the sign of any basis vector wrong and Revit throws — but the exception's `.Message` comes back
  **empty string**, not a helpful description, so it silently looks like "no reason given" unless you
  also print `ex.GetType().Name` and `ex.StackTrace` to diagnose it.
- Concretely: `BasisX` = the section's left-right (in-view) axis, `BasisY` = up (usually global
  `XYZ.BasisZ`), `BasisZ` = the direction the camera looks (into the cut). Pick `BasisX` and the look
  direction independently for each of two perpendicular sections through the same plan and check the
  cross product by hand — flipping which world axis maps to `BasisX` to get the "look" direction you want
  (e.g. looking west instead of east) also flips whether the resulting transform is still right-handed;
  it isn't automatic, you may need to negate `BasisX` (not `BasisZ`) to restore a valid basis while
  keeping the same look direction.
- After creating a section (or any new view), do a fresh read-back (`FilteredElementCollector(doc,
  view.Id)` element count) before calling it done — confirms the crop/depth actually captures real model
  content instead of an accidentally-empty slice.

