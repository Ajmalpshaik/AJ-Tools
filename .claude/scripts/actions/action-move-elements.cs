// ============================================================
// FRAGMENT (action) — action-move-elements.cs
// PURPOSE: Translate every element in `elements` by one offset vector (mm, X/Y/Z) — e.g. shift a room's
//          air terminals 300mm toward the wall, or nudge a mis-placed group of equipment.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with Ajmal before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double offsetXmm = 0.0;
double offsetYmm = 0.0;
double offsetZmm = 0.0;
// ---- END INPUTS ----

XYZ translation = new XYZ(
    UnitUtils.ConvertToInternalUnits(offsetXmm, DisplayUnitType.DUT_MILLIMETERS),
    UnitUtils.ConvertToInternalUnits(offsetYmm, DisplayUnitType.DUT_MILLIMETERS),
    UnitUtils.ConvertToInternalUnits(offsetZmm, DisplayUnitType.DUT_MILLIMETERS));

int moved = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Move Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            try
            {
                ElementTransformUtils.MoveElement(Document, e.Id, translation);
                moved++;
            }
            catch { skipped++; } // element type doesn't support move (e.g. some hosted/system elements)
        }
        t.Commit();
        sb.AppendLine($"Moved {moved} element(s) by ({offsetXmm}mm, {offsetYmm}mm, {offsetZmm}mm), skipped {skipped}.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to move — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
