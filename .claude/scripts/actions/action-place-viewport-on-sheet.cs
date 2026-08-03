// ============================================================
// FRAGMENT (action) — action-place-viewport-on-sheet.cs
// PURPOSE: Place each view in `elements` onto one target sheet as a Viewport, at a given point (or
//          centered on the sheet's title block if no point given).
// GOTCHA: a normal view (plan/section/elevation/3D) can only be placed on ONE sheet at a time — already-
//          placed views are skipped, never moved. Schedules/legends are different (see the two other
//          sheet-placement fragments) — they CAN appear on multiple sheets.
// ASSUMES: elements (List<Element>) are View objects (from a filter/creator), sb (StringBuilder) exists.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetSheetNumberText = "A-101";
double? pointXmm = null, pointYmm = null; // null = center of the sheet's title block outline
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
    using (var t = new Transaction(Document, "AJ Tools - Place Viewport On Sheet"))
    {
        t.Start();
        try
        {
            foreach (var el in elements)
            {
                var view = el as View;
                if (view == null || !Viewport.CanAddViewToSheet(Document, targetSheet.Id, view.Id)) { skipped++; continue; }

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
                    point = new XYZ((outline.Min.U + outline.Max.U) / 2, (outline.Min.V + outline.Max.V) / 2, 0);
                }

                Viewport.Create(Document, targetSheet.Id, view.Id, point);
                placed++;
            }
            t.Commit();
            sb.AppendLine($"Placed {placed} viewport(s) on sheet '{targetSheet.SheetNumber} - {targetSheet.Name}', skipped {skipped} (already placed elsewhere, or not a placeable view type).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to place viewports — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
