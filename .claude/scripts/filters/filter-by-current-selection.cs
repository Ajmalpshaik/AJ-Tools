// ============================================================
// FRAGMENT (filter) — filter-by-current-selection.cs
// PURPOSE: Use whatever Ajmal currently has selected in Revit as the element set — for "do this to
//          what I've got selected" requests, no category/family/size logic needed at all.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

var sb = new System.Text.StringBuilder();

List<Element> elements = UIDocument.Selection.GetElementIds()
    .Select(id => Document.GetElement(id))
    .Where(e => e != null)
    .ToList();

sb.AppendLine($"Filtered {elements.Count} currently-selected element(s).");
if (elements.Count == 0)
{
    sb.AppendLine("Nothing is selected in Revit right now — ask Ajmal to select elements first.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
