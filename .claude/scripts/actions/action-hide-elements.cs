// ============================================================
// FRAGMENT (action) — action-hide-elements.cs
// PURPOSE: Hide exactly `elements` in the active view (the opposite of isolate — everything else stays
//          visible, these disappear). Temporary by default (matches house convention — permanent
//          visibility changes need an explicit ask, see ../knowledge/live-model/views.md); set `permanent = true`
//          only when the user has actually asked for a lasting change, not a look-at-this-for-now hide.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: ../knowledge/live-model/views.md § View visibility patterns
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool permanent = false; // false = HideElementsTemporary (default); true = permanent View.HideElements
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Hide Elements"))
    {
        t.Start();
        try
        {
            var ids = elements.Select(e => e.Id).ToList();
            if (permanent)
            {
                view.HideElements(ids);
            }
            else
            {
                view.HideElementsTemporary(ids);
            }
            t.Commit();
            string mode = permanent ? "permanently" : "temporarily (Reset Temporary Hide/Isolate clears it)";
            sb.AppendLine($"Hid {elements.Count} element(s) {mode} in '{view.Name}'.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to hide — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
