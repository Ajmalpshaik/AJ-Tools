// ============================================================
// FRAGMENT (action) — action-reset-graphic-overrides.cs
// PURPOSE: Clear graphic overrides (color, fill pattern) on every element in `elements` — the paired
//          "undo" for action-set-color-uniform.cs / action-color-by-group.cs when the user wants the
//          coloring removed without a full Revit Undo (e.g. it's from several turns back).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

View resetView = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (resetView == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Reset Graphic Overrides"))
    {
        t.Start();
        try
        {
            var blank = new OverrideGraphicSettings();
            foreach (var e in elements)
            {
                resetView.SetElementOverrides(e.Id, blank);
            }
            t.Commit();
            sb.AppendLine($"Reset graphic overrides on {elements.Count} element(s) in view '{resetView.Name}'.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to reset overrides — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
