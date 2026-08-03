// ============================================================
// FRAGMENT (action) — action-rotate-elements.cs
// PURPOSE: Rotate every element in `elements` around a vertical axis by one angle (degrees) — e.g. spin
//          a mis-oriented piece of equipment 90 degrees, or rotate a cluster of terminals to match a new
//          room layout.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with Ajmal before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double angleDegrees = 90.0;                // positive = counterclockwise, looking down from above
double? pivotXmm = null, pivotYmm = null;  // null = rotate each element about its own location point
// ---- END INPUTS ----

double angleRadians = angleDegrees * Math.PI / 180.0;
int rotated = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Rotate Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            XYZ pivot;
            if (pivotXmm.HasValue && pivotYmm.HasValue)
            {
                pivot = new XYZ(
                    UnitUtils.ConvertToInternalUnits(pivotXmm.Value, DisplayUnitType.DUT_MILLIMETERS),
                    UnitUtils.ConvertToInternalUnits(pivotYmm.Value, DisplayUnitType.DUT_MILLIMETERS),
                    0);
            }
            else if (e.Location is LocationPoint lp)
            {
                pivot = lp.Point;
            }
            else if (e.Location is LocationCurve lc)
            {
                pivot = lc.Curve.Evaluate(0.5, true);
            }
            else { skipped++; continue; }

            var axis = Line.CreateBound(pivot, pivot + XYZ.BasisZ);
            try
            {
                ElementTransformUtils.RotateElement(Document, e.Id, axis, angleRadians);
                rotated++;
            }
            catch { skipped++; }
        }
        t.Commit();
        sb.AppendLine($"Rotated {rotated} element(s) by {angleDegrees} degrees, skipped {skipped}.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to rotate — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
