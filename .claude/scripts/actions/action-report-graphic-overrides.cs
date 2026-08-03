// ============================================================
// FRAGMENT (action) — action-report-graphic-overrides.cs
// PURPOSE: Read back the current graphic overrides on every element in `elements`, in the active view —
//          projection/cut line color, surface fill pattern color + visibility, transparency, halftone.
//          Read-only — pairs with the Set actions (action-set-color-uniform.cs, action-color-by-group.cs,
//          action-set-transparency.cs, action-reset-graphic-overrides.cs), which only ever SET; this is
//          the "what's actually on it right now" check, per element/object.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int maxRows = 50;
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

Func<Autodesk.Revit.DB.Color, string> colorText = c => (c != null && c.IsValid) ? $"RGB({c.Red},{c.Green},{c.Blue})" : "(none)";

var view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    sb.AppendLine($"Graphic overrides in view '{view.Name}' for {Math.Min(elements.Count, maxRows)} of {elements.Count} element(s):");

    foreach (var e in elements.Take(maxRows))
    {
        OverrideGraphicSettings ogs;
        try { ogs = view.GetElementOverrides(e.Id); } catch { sb.AppendLine($"  Id {e.Id.IntegerValue}: could not read overrides."); continue; }

        // NOTE: IsSurfaceForegroundPatternVisible/IsCutForegroundPatternVisible default to TRUE even with
        // nothing overridden — they are not a signal of an actual override, only whether a pattern (if any
        // is set) would show. The real signal is a valid color or a real pattern Id.
        bool hasAnyOverride = ogs.ProjectionLineColor.IsValid || ogs.CutLineColor.IsValid
            || ogs.SurfaceForegroundPatternColor.IsValid || ogs.CutForegroundPatternColor.IsValid
            || ogs.SurfaceForegroundPatternId != ElementId.InvalidElementId
            || ogs.CutForegroundPatternId != ElementId.InvalidElementId
            || ogs.Transparency != 0 || ogs.Halftone;

        if (!hasAnyOverride)
        {
            sb.AppendLine($"  Id {e.Id.IntegerValue}: no overrides.");
            continue;
        }

        sb.AppendLine($"  Id {e.Id.IntegerValue}: projection line {colorText(ogs.ProjectionLineColor)}, cut line {colorText(ogs.CutLineColor)}, " +
            $"surface pattern color {colorText(ogs.SurfaceForegroundPatternColor)} (visible: {ogs.IsSurfaceForegroundPatternVisible}), " +
            $"transparency {ogs.Transparency}%, halftone {ogs.Halftone}.");
    }
    if (elements.Count > maxRows)
        sb.AppendLine($"... {elements.Count - maxRows} more element(s) not shown.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
