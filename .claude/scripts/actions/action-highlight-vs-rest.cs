// ============================================================
// FRAGMENT (action) — action-highlight-vs-rest.cs
// PURPOSE: Color every element ALREADY IN THE ACTIVE VIEW gray, except `elements` (the filtered
//          highlight subset, from a filter fragment above), which gets its own highlight color instead.
//          Different from action-set-color-uniform.cs (colors only `elements`, leaves everything else
//          untouched) and action-color-by-group.cs (splits `elements` itself into many colored
//          sub-groups) — this one is specifically "make ONE subset stand out against a dimmed rest of
//          the whole model."
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
//          `elements` should be a SUBSET of the active view's content (e.g. one family within a
//          category) — if it's the whole model already there's nothing left to gray out.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: verified live 2026-07-15 — "color VCD red, everything else in the model gray" (3761 grayed,
//         35 red, 0 skipped, 3796 total elements in the active view).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
byte highlightR = 255, highlightG = 0, highlightB = 0;   // e.g. red
byte restR = 128, restG = 128, restB = 128;              // e.g. gray
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

var view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    var solidFillPattern = new FilteredElementCollector(Document)
        .OfClass(typeof(FillPatternElement))
        .Cast<FillPatternElement>()
        .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

    OverrideGraphicSettings MakeOgs(Autodesk.Revit.DB.Color c)
    {
        var ogs = new OverrideGraphicSettings();
        ogs.SetProjectionLineColor(c);
        ogs.SetCutLineColor(c);
        if (solidFillPattern != null)
        {
            ogs.SetSurfaceForegroundPatternColor(c);
            ogs.SetSurfaceForegroundPatternId(solidFillPattern.Id);
            ogs.SetSurfaceForegroundPatternVisible(true);
            ogs.SetCutForegroundPatternColor(c);
            ogs.SetCutForegroundPatternId(solidFillPattern.Id);
            ogs.SetCutForegroundPatternVisible(true);
        }
        return ogs;
    }

    var highlightOgs = MakeOgs(new Autodesk.Revit.DB.Color(highlightR, highlightG, highlightB));
    var restOgs = MakeOgs(new Autodesk.Revit.DB.Color(restR, restG, restB));
    var highlightIds = new HashSet<ElementId>(elements.Select(e => e.Id));

    var allInView = new FilteredElementCollector(Document, view.Id)
        .WhereElementIsNotElementType()
        .ToElements();

    int highlightCount = 0, restCount = 0, skipCount = 0;

    using (var t = new Transaction(Document, "AJ Tools - Highlight Vs Rest"))
    {
        t.Start();
        try
        {
            foreach (var e in allInView)
            {
                bool isHighlight = highlightIds.Contains(e.Id);
                try
                {
                    view.SetElementOverrides(e.Id, isHighlight ? highlightOgs : restOgs);
                    if (isHighlight) highlightCount++; else restCount++;
                }
                catch
                {
                    skipCount++; // element/category doesn't support graphic overrides in this view — skip, don't fail the whole batch
                }
            }
            t.Commit();
            sb.AppendLine($"Highlighted {highlightCount} element(s) RGB({highlightR},{highlightG},{highlightB}), grayed {restCount} RGB({restR},{restG},{restB}) in view '{view.Name}' ({skipCount} skipped).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to highlight vs rest — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
