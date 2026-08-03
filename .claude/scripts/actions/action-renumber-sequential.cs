// ============================================================
// FRAGMENT (action) — action-renumber-sequential.cs
// PURPOSE: Assign a sequential value to one parameter across `elements` — e.g. renumber rooms
//          101, 102, 103..., renumber sheets, or restamp a Mark parameter in order. Sort order and the
//          numbering pattern (prefix/padding/suffix) are both inputs, not fixed.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// GOTCHA: position-based sorting only works for physically located elements (rooms, equipment, family
//         instances). Sheets/views have no Location — use sortMode = "existing_value" for those, or the
//         elements will keep whatever order the filter collected them in.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with Ajmal before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetParameterName = "Number";  // e.g. "Number" (Rooms), "Sheet Number" (Sheets), "Mark"
string sortMode = "position_x";         // "position_x" | "position_y" | "existing_value"
int startNumber = 1;
int increment = 1;
string prefix = "";
string suffix = "";
int padWidth = 0;                       // 0 = no zero-padding, e.g. 3 -> "001"
// ---- END INPUTS ----

Func<Element, XYZ> getPoint = e =>
{
    if (e.Location is LocationPoint lp) return lp.Point;
    if (e.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
    try { var bb = e.get_BoundingBox(null); if (bb != null) return (bb.Min + bb.Max) * 0.5; } catch { }
    return XYZ.Zero;
};

IEnumerable<Element> ordered;
if (sortMode == "position_x") ordered = elements.OrderBy(e => getPoint(e).X).ThenBy(e => getPoint(e).Y);
else if (sortMode == "position_y") ordered = elements.OrderByDescending(e => getPoint(e).Y).ThenBy(e => getPoint(e).X);
else ordered = elements.OrderBy(e => e.LookupParameter(targetParameterName)?.AsValueString() ?? e.Name ?? "");

var orderedList = ordered.ToList();
int updated = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Renumber Sequential"))
{
    t.Start();
    try
    {
        int n = startNumber;
        foreach (var e in orderedList)
        {
            var p = e.LookupParameter(targetParameterName);
            if (p == null || p.IsReadOnly || p.StorageType != StorageType.String) { skipped++; n += increment; continue; }

            string numberText = padWidth > 0 ? n.ToString().PadLeft(padWidth, '0') : n.ToString();
            p.Set($"{prefix}{numberText}{suffix}");
            updated++;
            n += increment;
        }
        t.Commit();
        sb.AppendLine($"Renumbered {updated} element(s) on '{targetParameterName}', sorted by {sortMode}, starting at {startNumber} (skipped {skipped}).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to renumber — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
