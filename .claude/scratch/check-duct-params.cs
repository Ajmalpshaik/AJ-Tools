var sb = new System.Text.StringBuilder();

var duct = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctCurves)
    .WhereElementIsNotElementType()
    .Cast<MEPCurve>()
    .FirstOrDefault();

if (duct != null) {
    var wParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
    var hParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
    var flowParam = duct.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
    var velParam = duct.get_Parameter(BuiltInParameter.RBS_VELOCITY);
    var fricParam = duct.get_Parameter(BuiltInParameter.RBS_FRICTION);
    
    sb.AppendLine($"Duct ID: {duct.Id}");
    sb.AppendLine($"Width (internal): {wParam?.AsDouble()}");
    sb.AppendLine($"Height (internal): {hParam?.AsDouble()}");
    sb.AppendLine($"Flow (internal): {flowParam?.AsDouble()}");
    sb.AppendLine($"Velocity (internal): {velParam?.AsDouble()}");
    sb.AppendLine($"Friction (internal): {fricParam?.AsDouble()}");
}

return sb.ToString();
