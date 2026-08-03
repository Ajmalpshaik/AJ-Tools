// ============================================================
// FRAGMENT (action) — action-place-schedule-on-sheet.cs
// PURPOSE: Place each schedule in `elements` onto one target sheet. Unlike a normal view, the SAME
//          schedule CAN be placed on multiple sheets — "copy a schedule to another sheet" is just
//          running this again with a different target, not duplicating the schedule itself.
// ASSUMES: elements (List<Element>) are ViewSchedule objects, sb (StringBuilder) exists.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetSheetNumberText = "A-101";
double? pointXmm = null, pointYmm = null; // null = near the sheet's top-left corner
// ---- END INPUTS ----

var targetSheet = new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
    .FirstOrDefault(s => s.SheetNumber.Equals(targetSheetNumberText, StringComparison.OrdinalIgnoreCase));

int placed = 0, skipped = 0;

if (targetSheet == null)
{
    sb.AppendLine($"Target sheet '{targetSheetNumberText}' not found — nothing placed.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Place Schedule On Sheet"))
    {
        t.Start();
        try
        {
            foreach (var el in elements)
            {
                var schedule = el as ViewSchedule;
                if (schedule == null || schedule.IsTitleblockRevisionSchedule) { skipped++; continue; }

                XYZ point;
                if (pointXmm.HasValue && pointYmm.HasValue)
                {
                    point = new XYZ(
                        UnitUtils.ConvertToInternalUnits(pointXmm.Value, DisplayUnitType.DUT_MILLIMETERS),
                        UnitUtils.ConvertToInternalUnits(pointYmm.Value, DisplayUnitType.DUT_MILLIMETERS),
                        0);
                }
                else
                {
                    var outline = targetSheet.Outline;
                    point = new XYZ(outline.Min.U + 0.2, outline.Max.V - 0.2, 0);
                }

                try
                {
                    ScheduleSheetInstance.Create(Document, targetSheet.Id, schedule.Id, point);
                    placed++;
                }
                catch { skipped++; } // key schedules / already-placed-here can throw — skip, don't fail the batch
            }
            t.Commit();
            sb.AppendLine($"Placed {placed} schedule(s) on sheet '{targetSheet.SheetNumber} - {targetSheet.Name}', skipped {skipped}.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to place schedules — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
