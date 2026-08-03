// ============================================================
// FRAGMENT (filter) — filter-by-category-name.cs
// PURPOSE: Every instance of a category, resolved by its plain display NAME ("Ducts", "Duct
//          Accessories", "Mechanical Equipment") instead of requiring the exact BuiltInCategory enum
//          member — more dictation-friendly, since Ajmal names categories in plain words, not enum
//          identifiers. Prefer filter-by-category.cs when the BuiltInCategory is already known; use
//          this one when working straight from Ajmal's own wording and you'd otherwise have to guess
//          the enum name.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: category-by-name resolution adapted from an external repository's
//         ColorSplashEventHandler — only the technique was taken, not any code.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string categoryName = "Ducts";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>();

Category category = null;
foreach (Category cat in Document.Settings.Categories)
{
    if (cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
    {
        category = cat;
        break;
    }
}

if (category == null)
{
    sb.AppendLine($"Category '{categoryName}' not found — check the exact Revit category name and try again.");
}
else
{
    elements = new FilteredElementCollector(Document)
        .OfCategoryId(category.Id)
        .WhereElementIsNotElementType()
        .ToList();
    sb.AppendLine($"Filtered {elements.Count} element(s) in category '{category.Name}'.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
