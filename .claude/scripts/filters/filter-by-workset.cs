// ============================================================
// FRAGMENT (filter) - filter-by-workset.cs
// PURPOSE: Collect elements that belong to one user workset, optionally limited to one or more
//          categories. Use this for "what is in workset X", "select links in Linked Models workset",
//          and workset-based checking before view or graphics actions.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE - see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: AJ Tools Workset 3D Views / Set Link Workset patterns.
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
string worksetName = "Linked Models";
BuiltInCategory[] categoryScope = new BuiltInCategory[0]; // empty = every category
bool activeViewOnly = false;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>();

if (!Document.IsWorkshared)
{
    sb.AppendLine("This model is not workshared, so it has no user worksets to filter.");
}
else
{
    Workset targetWorkset = new FilteredWorksetCollector(Document)
        .OfKind(WorksetKind.UserWorkset)
        .FirstOrDefault(ws => ws.Name.Equals(worksetName, StringComparison.OrdinalIgnoreCase));

    if (targetWorkset == null)
    {
        sb.AppendLine($"Workset '{worksetName}' not found.");
    }
    else
    {
        var collector = activeViewOnly
            ? new FilteredElementCollector(Document, Document.ActiveView.Id)
            : new FilteredElementCollector(Document);

        IEnumerable<Element> query;
        if (categoryScope.Length > 0)
        {
            var categoryIds = categoryScope.Select(c => new ElementId(c)).ToList();
            query = collector
                .WherePasses(new ElementMulticategoryFilter(categoryIds))
                .WhereElementIsNotElementType();
        }
        else
        {
            query = collector.WhereElementIsNotElementType();
        }

        elements = query
            .Where(e =>
            {
                if (e == null)
                    return false;

                try { return e.WorksetId == targetWorkset.Id; }
                catch { return false; }
            })
            .ToList();

        string scopeLabel = activeViewOnly ? $"active view '{Document.ActiveView.Name}'" : "whole model";
        string categoryLabel = categoryScope.Length == 0 ? "all categories" : string.Join(", ", categoryScope.Select(c => c.ToString()));
        sb.AppendLine($"Filtered {elements.Count} element(s) on workset '{targetWorkset.Name}', categories: {categoryLabel}, scope: {scopeLabel}.");
    }
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
