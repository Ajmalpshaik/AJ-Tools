// ============================================================
// FRAGMENT (action) — action-report-location.cs
// PURPOSE: Report each element's position — a point for point-based elements (equipment, terminals,
//          rooms), or the endpoints for line-based elements (ducts, pipes, walls). Falls back to the
//          bounding-box center for anything with neither. Read-only.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int maxRows = 50;
// ---- END INPUTS ----

Func<double, double> mm = ft => UnitUtils.ConvertFromInternalUnits(ft, DisplayUnitType.DUT_MILLIMETERS);

sb.AppendLine($"Location for {Math.Min(elements.Count, maxRows)} of {elements.Count} element(s), in mm:");
foreach (var e in elements.Take(maxRows))
{
    if (e.Location is LocationPoint lp)
    {
        var p = lp.Point;
        sb.AppendLine($"  Id {e.Id.IntegerValue}: point ({mm(p.X):F0}, {mm(p.Y):F0}, {mm(p.Z):F0})");
    }
    else if (e.Location is LocationCurve lc)
    {
        var p0 = lc.Curve.GetEndPoint(0);
        var p1 = lc.Curve.GetEndPoint(1);
        sb.AppendLine($"  Id {e.Id.IntegerValue}: line from ({mm(p0.X):F0}, {mm(p0.Y):F0}, {mm(p0.Z):F0}) to ({mm(p1.X):F0}, {mm(p1.Y):F0}, {mm(p1.Z):F0})");
    }
    else
    {
        var bb = e.get_BoundingBox(null);
        if (bb != null)
        {
            var c = (bb.Min + bb.Max) * 0.5;
            sb.AppendLine($"  Id {e.Id.IntegerValue}: no direct location — bounding-box center ({mm(c.X):F0}, {mm(c.Y):F0}, {mm(c.Z):F0})");
        }
        else
        {
            sb.AppendLine($"  Id {e.Id.IntegerValue}: no location or bounding box available.");
        }
    }
}
if (elements.Count > maxRows) sb.AppendLine($"... {elements.Count - maxRows} more element(s) not shown.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
