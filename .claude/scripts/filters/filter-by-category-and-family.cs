// ============================================================
// FRAGMENT (filter) — filter-by-category-and-family.cs
// PURPOSE: One category, narrowed to instances whose family name contains a string. The VCD-style
//          filter (VCD is a family inside Duct Accessories, not its own category — see glossary.md).
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// If the exact FamilySymbol's ElementId is already known (e.g. from a prior query in the same
// composed script), set familySymbolId instead of familyNameContains — this uses Revit's own
// FamilyInstanceFilter pushed into the collector, which is faster than a LINQ name match and exact
// rather than substring-based (adapted from an external repository's
// AIElementFilterEventHandler — only the technique was taken, not any code).

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctAccessory;
string familyNameContains = "VCD";       // used when familySymbolId is not set
ElementId familySymbolId = ElementId.InvalidElementId; // set this to skip the name match entirely
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements;

if (familySymbolId != ElementId.InvalidElementId)
{
    var familyFilter = new FamilyInstanceFilter(Document, familySymbolId);
    elements = new FilteredElementCollector(Document)
        .OfCategory(targetCategory)
        .WhereElementIsNotElementType()
        .WherePasses(familyFilter)
        .ToList();
    sb.AppendLine($"Filtered {elements.Count} element(s), exact family type id {familySymbolId}.");
}
else
{
    elements = new FilteredElementCollector(Document)
        .OfCategory(targetCategory)
        .WhereElementIsNotElementType()
        .Cast<FamilyInstance>()
        .Where(fi => fi.Symbol?.Family?.Name?.IndexOf(familyNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
        .Cast<Element>()
        .ToList();
    sb.AppendLine($"Filtered {elements.Count} element(s), family name contains '{familyNameContains}'.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
