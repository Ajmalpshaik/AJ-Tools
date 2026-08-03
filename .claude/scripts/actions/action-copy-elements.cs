// ============================================================
// FRAGMENT (action) — action-copy-elements.cs
// PURPOSE: Duplicate every element in `elements`, offset by one vector (mm, X/Y/Z) — e.g. copy a row of
//          air terminals to a mirrored room, or duplicate equipment to a new position.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — the freshly created copies, for chaining into another
//           action (e.g. select just the new copies, or set a parameter on just the copies).
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

var newElementIds = new List<ElementId>();
int copied = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Copy Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            try
            {
                var ids = ElementTransformUtils.CopyElement(Document, e.Id, translation);
                newElementIds.AddRange(ids);
                copied += ids.Count;
            }
            catch { skipped++; } // element type doesn't support copy
        }
        t.Commit();
        sb.AppendLine($"Copied {copied} new element(s) from {elements.Count} source(s), offset by ({offsetXmm}mm, {offsetYmm}mm, {offsetZmm}mm), skipped {skipped} source(s).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to copy — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
