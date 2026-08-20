// ============================================================
// FRAGMENT (action) — action-section-box-and-zoom.cs
// PURPOSE: Build a bounding box around every element in `elements`, apply it as a 3D view's section
//          box (finds/creates a usable default 3D view if the active view isn't one), and zoom/show
//          the elements — for "let me see these in 3D" requests, distinct from isolate/select which
//          stay in whatever 2D view is active.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double marginMm = 1000; // extra clearance around the elements' combined bounding box
int? targetViewIdInt = null; // null = active view (or the first usable 3D view if active isn't 3D); set an Element Id to target a specific 3D view directly
// ---- END INPUTS ----

if (elements.Count == 0)
{
    sb.AppendLine("No elements to section-box — nothing to do.");
}
else
{
    View3D targetView = targetViewIdInt.HasValue
        ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View3D
        : Document.ActiveView as View3D;
    if (targetView == null && !targetViewIdInt.HasValue)
    {
        targetView = new FilteredElementCollector(Document)
            .OfClass(typeof(View3D))
            .Cast<View3D>()
            .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked
                && (v.Name.Contains("{3D}") || v.Name.Contains("Default 3D")));
    }

    if (targetView == null)
    {
        sb.AppendLine("No usable 3D view found to apply a section box to — nothing changed.");
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
            sb.AppendLine("None of the selected elements have a bounding box — nothing changed.");
        }
        else
        {
            double marginFt = UnitUtils.ConvertToInternalUnits(marginMm, DisplayUnitType.DUT_MILLIMETERS);
            box.Min -= new XYZ(marginFt, marginFt, marginFt);
            box.Max += new XYZ(marginFt, marginFt, marginFt);

            using (var t = new Transaction(Document, "AJ Tools - Section Box"))
            {
                t.Start();
                try
                {
                    targetView.IsSectionBoxActive = true;
                    targetView.SetSectionBox(box);
                    t.Commit();
                    UIDocument.ActiveView = targetView;
                    UIDocument.ShowElements(elements.Select(e => e.Id).ToList());
                    sb.AppendLine($"Section-boxed and zoomed to {elements.Count} element(s) in 3D view '{targetView.Name}'.");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED to apply section box — rolled back, nothing changed. Reason: {ex.Message}");
                }
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
