// AJ Tools Connector tool: Unhide All
//
// Runs inside the connector via Roslyn. Available by name: Document, UIDocument, Application,
// UIApplication. Already imported: System, System.Linq, System.Collections.Generic,
// Autodesk.Revit.DB, Autodesk.Revit.UI.
//
// A multi-statement script MUST end with an explicit `return` - a trailing expression without a
// semicolon does not reliably produce output in this hosting setup.
//
// Behaviour matches AJ Tools' own Unhide All command: a full-model scan (a view-scoped collector can
// exclude the very elements we are looking for), then one transaction that unhides them and clears
// Temporary Hide/Isolate if it is on.

var view = Document.ActiveView;
if (view == null) return "No active view.";

var hidden = new List<ElementId>();

foreach (Element element in new FilteredElementCollector(Document).WhereElementIsNotElementType())
{
    if (element == null || !element.IsValidObject) continue;

    try
    {
        if (element.IsHidden(view)) hidden.Add(element.Id);
    }
    catch
    {
        // Some element types refuse IsHidden in some views. Not hidden as far as we are concerned.
    }
}

bool temporaryCleared = false;

using (var transaction = new Transaction(Document, "AJ Tools: Unhide All"))
{
    transaction.Start();

    if (hidden.Count > 0) view.UnhideElements(hidden);

    try
    {
        view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
        temporaryCleared = true;
    }
    catch
    {
        // Temporary Hide/Isolate was not active in this view.
    }

    transaction.Commit();
}

string elementsLine = hidden.Count > 0
    ? hidden.Count + " hidden element(s) restored."
    : "No permanently hidden elements found.";

string temporaryLine = temporaryCleared
    ? "Temporary Hide/Isolate cleared."
    : "Temporary Hide/Isolate was not active.";

return elementsLine + "\n" + temporaryLine;
