// ============================================================
// FRAGMENT (action) - action-show-elements.cs
// PURPOSE: Zoom/show the current `elements` in Revit, optionally also making them the active
//          selection. Use this after a count/filter when Ajmal says "show me", "zoom to it",
//          "where is it", or "select and show".
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE - see .claude/scripts/README.md for how to compose.
// SOURCE: AJ Tools linked element search pattern (`Selection.SetElementIds` + `ShowElements`).
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
bool alsoSelect = true;
int maxElementsToShow = 500;
// ---- END INPUTS ----

if (elements.Count == 0)
{
    sb.AppendLine("No elements to show.");
}
else
{
    var ids = elements
        .Where(e => e != null && e.IsValidObject)
        .Take(maxElementsToShow)
        .Select(e => e.Id)
        .ToList();

    try
    {
        if (alsoSelect)
            UIDocument.Selection.SetElementIds(ids);

        UIDocument.ShowElements(ids);

        string limited = elements.Count > ids.Count ? $" (limited to first {ids.Count})" : "";
        sb.AppendLine($"Showed {ids.Count} element(s){limited}{(alsoSelect ? " and selected them" : "")}.");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED to show elements. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
