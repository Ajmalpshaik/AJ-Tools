// ============================================================
// FRAGMENT (action) — action-unhide-elements.cs
// PURPOSE: Reverse a PERMANENT per-element hide (View.HideElements) on every element in `elements` —
//          distinct from resetting temporary isolate/hide, which is a separate view-mode toggle, not
//          an element-by-element property. Use this only for elements that were permanently hidden.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Unhide Elements"))
    {
        t.Start();
        try
        {
            view.UnhideElements(elements.Select(e => e.Id).ToList());
            t.Commit();
            sb.AppendLine($"Unhid {elements.Count} permanently-hidden element(s) in '{view.Name}'.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to unhide — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
