// ============================================================
// EXAMPLE — fully assembled, ready to run via run_csharp
// Matches Ajmal's own scenario verbatim: "change all the colors of the 500mm height duct, then
// isolate and select them."
// Assembled from: filters/filter-by-category-and-numeric-param.cs
//                + actions/action-set-color-uniform.cs
//                + actions/action-isolate-elements.cs
//                + actions/action-select-elements.cs
// This is what composing looks like in practice — copy this shape for any new filter+action(s)
// combination. See ../README.md for the full explanation.
// ============================================================

// ---- FILTER: category + numeric parameter ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
string parameterName = "Height";
double valueMm = 500;
string comparison = "eq";
double valueMaxMm = 0;
double toleranceMm = 1;

var sb = new System.Text.StringBuilder();

double valueFt = UnitUtils.ConvertToInternalUnits(valueMm, DisplayUnitType.DUT_MILLIMETERS);
double toleranceFt = UnitUtils.ConvertToInternalUnits(toleranceMm, DisplayUnitType.DUT_MILLIMETERS);
double valueMaxFt = UnitUtils.ConvertToInternalUnits(valueMaxMm, DisplayUnitType.DUT_MILLIMETERS);

List<Element> elements = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .Where(e =>
    {
        var p = e.LookupParameter(parameterName);
        if (p == null || p.StorageType != StorageType.Double) return false;
        double v = p.AsDouble();
        switch (comparison)
        {
            case "gte": return v >= valueFt;
            case "lte": return v <= valueFt;
            case "between": return v >= valueFt && v <= valueMaxFt;
            default: return Math.Abs(v - valueFt) <= toleranceFt;
        }
    })
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s) where {parameterName} {comparison} {valueMm}mm.");

// ---- ACTION 1: color uniform ----
byte colorR = 255, colorG = 0, colorB = 0;

var solidFillPattern = new FilteredElementCollector(Document)
    .OfClass(typeof(FillPatternElement))
    .Cast<FillPatternElement>()
    .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

using (var t1 = new Transaction(Document, "AJ Tools - Set Color Override"))
{
    t1.Start();
    try
    {
        var color = new Autodesk.Revit.DB.Color(colorR, colorG, colorB);
        foreach (var e in elements)
        {
            var ogs = Document.ActiveView.GetElementOverrides(e.Id);
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
            Document.ActiveView.SetElementOverrides(e.Id, ogs);
        }
        t1.Commit();
        sb.AppendLine($"Colored {elements.Count} element(s) RGB({colorR},{colorG},{colorB}) in view '{Document.ActiveView.Name}'.");
    }
    catch (Exception ex)
    {
        t1.RollBack();
        sb.AppendLine($"FAILED to set color — rolled back, nothing changed. Reason: {ex.Message}");
    }
}

// ---- ACTION 2: isolate ----
View view = Document.ActiveView;
if (!view.CanUseTemporaryVisibilityModes())
{
    sb.AppendLine($"View '{view.Name}' ({view.ViewType}) does not support temporary isolate — skipped.");
}
else
{
    using (var t2 = new Transaction(Document, "AJ Tools - Isolate Elements"))
    {
        t2.Start();
        try
        {
            if (view.IsTemporaryHideIsolateActive())
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            }
            view.IsolateElementsTemporary(elements.Select(e => e.Id).ToList());
            t2.Commit();
            sb.AppendLine($"Isolated {elements.Count} element(s) in '{view.Name}'.");
        }
        catch (Exception ex)
        {
            t2.RollBack();
            sb.AppendLine($"FAILED to isolate — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}

// ---- ACTION 3: select ----
UIDocument.Selection.SetElementIds(elements.Select(e => e.Id).ToList());
sb.AppendLine($"Selected {elements.Count} element(s) in Revit.");

// ---- FINAL RETURN (only the last fragment in a composed script needs this) ----
return sb.ToString();
