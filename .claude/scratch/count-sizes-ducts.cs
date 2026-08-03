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
    var groups = new Dictionary<string, int>();
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
        
        groups.TryGetValue(sizeLabel, out int existing);
        groups[sizeLabel] = existing + 1;
    }

    sb.AppendLine("Size (mm) | Qty");
    sb.AppendLine("--- | ---");
    foreach (var kv in groups.OrderByDescending(kv => kv.Value))
    {
        sb.AppendLine($"{kv.Key} | {kv.Value}");
    }
}

return sb.ToString();
