// ============================================================
// FRAGMENT (filter) — filter-by-id-list.cs
// PURPOSE: Look up a specific list of Element Ids Ajmal already has (pasted from a warning, read off a
//          tag, remembered from an earlier answer) and produce them as `elements` — for "what is this
//          element" / "what are these elements' parameters" without re-filtering another way.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended, plus a quick
//           Name/Category line per element so "what is this" is answered even without an action after it)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int[] elementIds = new int[] { };  // e.g. new[] { 937031, 938734 }
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();
var missing = new List<int>();

foreach (var id in elementIds)
{
    var e = Document.GetElement(new ElementId(id));
    if (e != null) elements.Add(e);
    else missing.Add(id);
}

sb.AppendLine($"Looked up {elementIds.Length} Id(s): found {elements.Count}, missing {missing.Count}" +
    (missing.Count > 0 ? $" ({string.Join(", ", missing)})" : "") + ".");
foreach (var e in elements)
    sb.AppendLine($"  Id {e.Id.IntegerValue}: '{e.Name}' — category: {e.Category?.Name ?? "(none)"}");
// ---- continue with one or more action fragments below (e.g. action-report-parameters.cs for full
// parameter values), or add return sb.ToString(); to stop here ----
