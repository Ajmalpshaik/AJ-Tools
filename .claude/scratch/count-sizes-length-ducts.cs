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

List<Element> elements = query.ToList();

if (elements.Count > 0)
{
    var groups = new Dictionary<string, (int Count, double TotalLength)>();
    foreach (var e in elements)
    {
        string sizeLabel = "unknown size";
        var widthP = e.LookupParameter("Width");
        var heightP = e.LookupParameter("Height");
        var diamP = e.LookupParameter("Diameter") ?? e.LookupParameter("Nominal Diameter");
        
        if (widthP != null && heightP != null && widthP.StorageType == StorageType.Double && heightP.StorageType == StorageType.Double) {
            double w = Math.Round(UnitUtils.ConvertFromInternalUnits(widthP.AsDouble(), DisplayUnitType.DUT_MILLIMETERS));
            double h = Math.Round(UnitUtils.ConvertFromInternalUnits(heightP.AsDouble(), DisplayUnitType.DUT_MILLIMETERS));
            sizeLabel = $"{w}x{h}mm";
        } else if (diamP != null && diamP.StorageType == StorageType.Double) {
            double d = Math.Round(UnitUtils.ConvertFromInternalUnits(diamP.AsDouble(), DisplayUnitType.DUT_MILLIMETERS));
            sizeLabel = $"Ø{d}mm";
        }
        
        double lengthMM = 0;
        var lengthP = e.LookupParameter("Length") ?? e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
        if (lengthP != null && lengthP.StorageType == StorageType.Double) {
            lengthMM = UnitUtils.ConvertFromInternalUnits(lengthP.AsDouble(), DisplayUnitType.DUT_MILLIMETERS);
        }
        
        groups.TryGetValue(sizeLabel, out var current);
        groups[sizeLabel] = (current.Count + 1, current.TotalLength + lengthMM);
    }

    sb.AppendLine("Size (mm) | Qty | Total Length (m)");
    sb.AppendLine("--- | --- | ---");
    foreach (var kv in groups.OrderByDescending(kv => kv.Value.Count))
    {
        double totalMeters = kv.Value.TotalLength / 1000.0;
        sb.AppendLine($"{kv.Key} | {kv.Value.Count} | {Math.Round(totalMeters, 2)}");
    }
}

return sb.ToString();
