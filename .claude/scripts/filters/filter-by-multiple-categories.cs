// ============================================================
// FRAGMENT (filter) - filter-by-multiple-categories.cs
// PURPOSE: Collect instances from several categories into one reusable element set. Use this for
//          system groups like "duct system", "pipe system", "cable tray system", or any request where
//          Ajmal names a group that maps to more than one Revit category.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE - see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: AJ Tools Pin Elements target groups and Autodesk FilteredElementCollector category filtering.
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
string groupLabel = "Duct System";
BuiltInCategory[] targetCategories =
{
    BuiltInCategory.OST_DuctCurves,
    BuiltInCategory.OST_DuctFitting,
    BuiltInCategory.OST_DuctAccessory,
    BuiltInCategory.OST_FlexDuctCurves,
    BuiltInCategory.OST_DuctInsulations,
    BuiltInCategory.OST_DuctLinings
};
bool activeViewOnly = false;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var collected = new List<Element>();
var seen = new HashSet<string>();
var skippedCategories = new List<string>();

foreach (BuiltInCategory category in targetCategories.Distinct())
{
    try
    {
        var collector = activeViewOnly
            ? new FilteredElementCollector(Document, Document.ActiveView.Id)
            : new FilteredElementCollector(Document);

        foreach (var e in collector.OfCategory(category).WhereElementIsNotElementType())
        {
            if (e == null || string.IsNullOrEmpty(e.UniqueId))
                continue;

            if (seen.Add(e.UniqueId))
                collected.Add(e);
        }
    }
    catch (Exception ex)
    {
        skippedCategories.Add($"{category}: {ex.Message}");
    }
}

List<Element> elements = collected;
string scopeLabel = activeViewOnly ? $"active view '{Document.ActiveView.Name}'" : "whole model";
sb.AppendLine($"Filtered {elements.Count} element(s) for {groupLabel} across {targetCategories.Distinct().Count()} categor(y/ies), scope: {scopeLabel}.");
if (skippedCategories.Count > 0)
{
    sb.AppendLine("Skipped unsupported categories: " + string.Join("; ", skippedCategories));
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
