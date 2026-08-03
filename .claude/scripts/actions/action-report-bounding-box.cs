// ============================================================
// FRAGMENT (action) — action-report-bounding-box.cs
// PURPOSE: Report each element's bounding box (min corner + size) in mm, plus the combined bounding box
//          of the whole set. Read-only.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int maxRows = 50;
bool reportCombined = true;
// ---- END INPUTS ----

Func<double, double> mm = ft => UnitUtils.ConvertFromInternalUnits(ft, DisplayUnitType.DUT_MILLIMETERS);

BoundingBoxXYZ combined = null;
sb.AppendLine($"Bounding box for {Math.Min(elements.Count, maxRows)} of {elements.Count} element(s), in mm:");

foreach (var e in elements.Take(maxRows))
{
    var bb = e.get_BoundingBox(null);
    if (bb == null) { sb.AppendLine($"  Id {e.Id.IntegerValue}: no bounding box available."); continue; }

    double w = mm(bb.Max.X - bb.Min.X), d = mm(bb.Max.Y - bb.Min.Y), h = mm(bb.Max.Z - bb.Min.Z);
    sb.AppendLine($"  Id {e.Id.IntegerValue}: size {w:F0} x {d:F0} x {h:F0} mm, min ({mm(bb.Min.X):F0}, {mm(bb.Min.Y):F0}, {mm(bb.Min.Z):F0})");

    if (reportCombined)
    {
        if (combined == null) combined = new BoundingBoxXYZ { Min = bb.Min, Max = bb.Max };
        else
        {
            combined.Min = new XYZ(Math.Min(combined.Min.X, bb.Min.X), Math.Min(combined.Min.Y, bb.Min.Y), Math.Min(combined.Min.Z, bb.Min.Z));
            combined.Max = new XYZ(Math.Max(combined.Max.X, bb.Max.X), Math.Max(combined.Max.Y, bb.Max.Y), Math.Max(combined.Max.Z, bb.Max.Z));
        }
    }
}

if (elements.Count > maxRows) sb.AppendLine($"... {elements.Count - maxRows} more element(s) not shown.");

if (reportCombined && combined != null)
{
    double cw = mm(combined.Max.X - combined.Min.X), cd = mm(combined.Max.Y - combined.Min.Y), ch = mm(combined.Max.Z - combined.Min.Z);
    sb.AppendLine($"Combined extents: {cw:F0} x {cd:F0} x {ch:F0} mm, min ({mm(combined.Min.X):F0}, {mm(combined.Min.Y):F0}, {mm(combined.Min.Z):F0})");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
