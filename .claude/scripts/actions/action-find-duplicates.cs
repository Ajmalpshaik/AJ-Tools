// ============================================================
// FRAGMENT (action) — action-find-duplicates.cs
// PURPOSE: Flag likely duplicate elements within `elements` — instances whose insertion points sit
//          within a small tolerance of each other (e.g. two air terminals stacked on the same spot).
//          Read-only QA check: reports and can select the flagged elements, never deletes or moves
//          anything — deleting stays a manual decision, never done from a script.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double toleranceMm = 50.0;   // insertion points within this distance count as "same spot"
bool selectFlagged = true;   // true = also set the flagged elements as the active Revit selection
// ---- END INPUTS ----

Func<Element, XYZ> getPoint = e =>
{
    if (e.Location is LocationPoint lp) return lp.Point;
    if (e.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
    try { var bb = e.get_BoundingBox(null); if (bb != null) return (bb.Min + bb.Max) * 0.5; } catch { }
    return null;
};

double toleranceFeet = UnitUtils.ConvertToInternalUnits(toleranceMm, DisplayUnitType.DUT_MILLIMETERS);
var points = elements.Select(e => (el: e, pt: getPoint(e))).Where(x => x.pt != null).ToList();

var groups = new List<List<Element>>();
var used = new bool[points.Count];
for (int i = 0; i < points.Count; i++)
{
    if (used[i]) continue;
    var cluster = new List<Element> { points[i].el };
    used[i] = true;
    for (int j = i + 1; j < points.Count; j++)
    {
        if (used[j]) continue;
        if (points[i].pt.DistanceTo(points[j].pt) <= toleranceFeet)
        {
            cluster.Add(points[j].el);
            used[j] = true;
        }
    }
    if (cluster.Count > 1) groups.Add(cluster);
}

var flagged = groups.SelectMany(g => g).ToList();
sb.AppendLine($"Checked {elements.Count} element(s) within {toleranceMm}mm tolerance: {groups.Count} duplicate cluster(s), {flagged.Count} element(s) flagged.");
foreach (var g in groups)
    sb.AppendLine("  Cluster: " + string.Join(", ", g.Select(e => e.Id.IntegerValue)));

if (selectFlagged && flagged.Count > 0)
{
    UIDocument.Selection.SetElementIds(flagged.Select(e => e.Id).ToList());
    sb.AppendLine($"Selected {flagged.Count} flagged element(s) in Revit for review.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
