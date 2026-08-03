// ============================================================
// FRAGMENT (action) — action-set-view-crop.cs
// PURPOSE: Set the ACTIVE view's crop region to fit `elements`' combined bounding box plus a margin,
//          turning crop on if it's off. Different from action-section-box-and-zoom.cs (that one is
//          3D-view-only); crop applies to plan/section/elevation views too. Different again from AJ
//          Tools' own compiled "View Crop" tool, which crops to a manually picked region — this crops
//          to a filtered element SET instead.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SIMPLIFICATION (same as action-section-box-and-zoom.cs): merges world-aligned bounding boxes directly;
//          a view with a rotated crop region would need the box transformed into the view's own
//          coordinate system first — not handled here, same known limitation as the 3D section-box action.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double marginMm = 500;
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

var view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else if (elements.Count == 0)
{
    sb.AppendLine("No elements to crop to — nothing to do.");
}
else
{
    BoundingBoxXYZ box = null;
    foreach (var e in elements)
    {
        var eb = e.get_BoundingBox(null);
        if (eb == null) continue;
        if (box == null)
        {
            box = new BoundingBoxXYZ { Min = eb.Min, Max = eb.Max };
        }
        else
        {
            box.Min = new XYZ(Math.Min(box.Min.X, eb.Min.X), Math.Min(box.Min.Y, eb.Min.Y), Math.Min(box.Min.Z, eb.Min.Z));
            box.Max = new XYZ(Math.Max(box.Max.X, eb.Max.X), Math.Max(box.Max.Y, eb.Max.Y), Math.Max(box.Max.Z, eb.Max.Z));
        }
    }

    if (box == null)
    {
        sb.AppendLine("None of the elements have a bounding box — nothing changed.");
    }
    else
    {
        double marginFt = UnitUtils.ConvertToInternalUnits(marginMm, DisplayUnitType.DUT_MILLIMETERS);
        box.Min -= new XYZ(marginFt, marginFt, 0);
        box.Max += new XYZ(marginFt, marginFt, 0);

        using (var t = new Transaction(Document, "AJ Tools - Set View Crop"))
        {
            t.Start();
            try
            {
                view.CropBoxActive = true;
                view.CropBoxVisible = true;
                view.CropBox = box;
                t.Commit();
                sb.AppendLine($"Cropped view '{view.Name}' to {elements.Count} element(s) + {marginMm}mm margin.");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to set view crop — rolled back, nothing changed. Reason: {ex.Message} (this view type may not support cropping).");
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
