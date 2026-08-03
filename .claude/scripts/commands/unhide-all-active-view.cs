// ============================================================
// SCRIPT: unhide-all-active-view.cs
// PURPOSE: Restore permanently hidden elements in the active view and clear Temporary Hide/Isolate.
//          This is a command, not a filter/action fragment, because hidden elements may not be returned
//          by a normal visible-view element filter.
// SOURCE: AJ Tools CmdUnhideAll pattern.
// ============================================================

var sb = new System.Text.StringBuilder();
View view = Document.ActiveView;
var hiddenIds = new List<ElementId>();

foreach (Element element in new FilteredElementCollector(Document).WhereElementIsNotElementType())
{
    if (element == null || !element.IsValidObject)
        continue;

    try
    {
        if (element.IsHidden(view))
            hiddenIds.Add(element.Id);
    }
    catch
    {
        // Some elements do not support hidden checks in every view; skip them.
    }
}

bool thiCleared = false;

using (var t = new Transaction(Document, "AJ Tools - Unhide All Active View"))
{
    t.Start();
    try
    {
        if (hiddenIds.Count > 0)
            view.UnhideElements(hiddenIds);

        try
        {
            view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            thiCleared = true;
        }
        catch
        {
            thiCleared = false;
        }

        t.Commit();
        sb.AppendLine(hiddenIds.Count > 0
            ? $"Restored {hiddenIds.Count} permanently hidden element(s) in '{view.Name}'."
            : $"No permanently hidden elements found in '{view.Name}'.");
        sb.AppendLine(thiCleared ? "Temporary Hide/Isolate cleared." : "Temporary Hide/Isolate was not active or could not be cleared.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to unhide active view - rolled back, nothing changed. Reason: {ex.Message}");
    }
}

return sb.ToString();
