// ============================================================
// FRAGMENT (action) — action-set-color-uniform.cs
// PURPOSE: Apply ONE color to every element in `elements` — both line color AND a solid surface fill
//          (the user explicitly wants both, not just line color, so it reads as that color in shaded/
//          realistic views too — see ../knowledge/live-model/mep-trace.md color-coding note).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
byte colorR = 255, colorG = 0, colorB = 0; // e.g. red
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    var solidFillPattern = new FilteredElementCollector(Document)
        .OfClass(typeof(FillPatternElement))
        .Cast<FillPatternElement>()
        .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

    using (var t = new Transaction(Document, "AJ Tools - Set Color Override"))
    {
        t.Start();
        try
        {
            var color = new Autodesk.Revit.DB.Color(colorR, colorG, colorB);
            foreach (var e in elements)
            {
                var ogs = view.GetElementOverrides(e.Id);
                ogs.SetProjectionLineColor(color);
                ogs.SetCutLineColor(color);
                if (solidFillPattern != null)
                {
                    ogs.SetSurfaceForegroundPatternColor(color);
                    ogs.SetSurfaceForegroundPatternId(solidFillPattern.Id);
                    ogs.SetSurfaceForegroundPatternVisible(true);
                    ogs.SetCutForegroundPatternColor(color);
                    ogs.SetCutForegroundPatternId(solidFillPattern.Id);
                    ogs.SetCutForegroundPatternVisible(true);
                }
                view.SetElementOverrides(e.Id, ogs);
            }
            t.Commit();
            sb.AppendLine($"Colored {elements.Count} element(s) RGB({colorR},{colorG},{colorB}) in view '{view.Name}'.");
        }
        catch (Exception ex)
        {
            // Explicit rollback with a clear reason in the report, rather than an uncommitted transaction
            // and a bare unhandled exception through the bridge.
            t.RollBack();
            sb.AppendLine($"FAILED to set color — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
