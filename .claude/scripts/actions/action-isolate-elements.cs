// ============================================================
// FRAGMENT (action) — action-isolate-elements.cs
// PURPOSE: Temporary-isolate exactly `elements` in the active view, resetting any prior isolation
//          first so it's never additive to a stale state.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: ../knowledge/live-model/views.md § View visibility patterns. The view-capability check uses Revit's own
//         View.CanUseTemporaryVisibilityModes() (adopted after seeing it in
//         github.com/mcp-servers-for-revit/revit-mcp-commandset) instead of a hand-maintained list of
//         ViewType enum values — more correct, since it asks Revit directly rather than guessing which
//         view types support isolation.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else if (!view.CanUseTemporaryVisibilityModes())
{
    sb.AppendLine($"View '{view.Name}' ({view.ViewType}) does not support temporary isolate — skipped.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Isolate Elements"))
    {
        t.Start();
        try
        {
            if (view.IsTemporaryHideIsolateActive())
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            }
            view.IsolateElementsTemporary(elements.Select(e => e.Id).ToList());
            t.Commit();
            sb.AppendLine($"Isolated {elements.Count} element(s) in '{view.Name}' (temporary — Reset Temporary Hide/Isolate clears it).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to isolate — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
