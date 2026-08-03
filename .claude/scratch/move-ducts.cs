BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
ElementId levelIdFilter = ElementId.InvalidElementId;

var sb = new System.Text.StringBuilder();

Func<Element, ElementId> resolveLevelId = e =>
{
    if (e is Wall wall) return wall.LevelId;
    if (e.LevelId != ElementId.InvalidElementId) return e.LevelId;
    var p = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
    return p?.AsElementId() ?? ElementId.InvalidElementId;
};

var query = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .AsEnumerable();

if (levelIdFilter != ElementId.InvalidElementId)
{
    query = query.Where(e => resolveLevelId(e) == levelIdFilter);
}

// Filter to ONLY 450x300mm ducts
List<Element> elements = new List<Element>();
foreach (var e in query)
{
    var widthP = e.LookupParameter("Width");
    var heightP = e.LookupParameter("Height");
    if (widthP != null && heightP != null && widthP.StorageType == StorageType.Double && heightP.StorageType == StorageType.Double) {
        double w = Math.Round(UnitUtils.ConvertFromInternalUnits(widthP.AsDouble(), DisplayUnitType.DUT_MILLIMETERS));
        double h = Math.Round(UnitUtils.ConvertFromInternalUnits(heightP.AsDouble(), DisplayUnitType.DUT_MILLIMETERS));
        if (w == 450 && h == 300) {
            elements.Add(e);
        }
    }
}

sb.AppendLine($"Found {elements.Count} 450x300mm duct(s).");

double offsetXmm = 24000.0;
double offsetYmm = 0.0;
double offsetZmm = 0.0;

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
            catch { skipped++; } 
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

return sb.ToString();
