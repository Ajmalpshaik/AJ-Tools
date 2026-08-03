// ============================================================
// FRAGMENT (filter) — filter-by-sheets.cs
// PURPOSE: Every ViewSheet in the model, optionally narrowed to sheet numbers containing a substring.
//          Use this whenever the job is "go through every sheet", not a normal model-element category.
// PRODUCES: elements (List<Element>, each one really a ViewSheet — cast back with `as ViewSheet` in
//           the action), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string sheetNumberContains = ""; // "" = every sheet; else substring match on SheetNumber, case-insensitive
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var query = new FilteredElementCollector(Document)
    .OfClass(typeof(ViewSheet))
    .Cast<ViewSheet>()
    .AsEnumerable();

if (!string.IsNullOrEmpty(sheetNumberContains))
{
    query = query.Where(s => s.SheetNumber.IndexOf(sheetNumberContains, StringComparison.OrdinalIgnoreCase) >= 0);
}

List<Element> elements = query.OrderBy(s => s.SheetNumber).Cast<Element>().ToList();
sb.AppendLine($"Filtered {elements.Count} sheet(s)." + (string.IsNullOrEmpty(sheetNumberContains) ? "" : $" (number contains \"{sheetNumberContains}\")"));
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
