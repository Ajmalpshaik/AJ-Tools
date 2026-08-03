var sb = new System.Text.StringBuilder();

var ducts = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctCurves)
    .WhereElementIsNotElementType()
    .Cast<MEPCurve>()
    .ToList();

int resized = 0;
// Default engineering parameters
double targetVelocityFPS = 1000.0 / 60.0; // 1000 FPM to Feet Per Second
double roundingIncrementFeet = 50.0 / 304.8; // 50 mm in feet
double minWidthFeet = 150.0 / 304.8; // Minimum 150 mm width

using (var t = new Transaction(Document, "AJ Tools - Resize All Ducts"))
{
    t.Start();
    foreach (var duct in ducts) {
        var flowParam = duct.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
        var heightParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
        var widthParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
        
        if (flowParam != null && heightParam != null && widthParam != null) {
            double flowCFS = flowParam.AsDouble();
            double heightFt = heightParam.AsDouble();
            
            if (flowCFS > 0 && heightFt > 0) {
                // Calculate required Area (sq ft)
                double requiredArea = flowCFS / targetVelocityFPS;
                
                // Calculate required Width (ft)
                double requiredWidth = requiredArea / heightFt;
                
                // Round up to the nearest 50 mm increment
                double numIncrements = requiredWidth / roundingIncrementFeet;
                double roundedWidth = Math.Ceiling(numIncrements) * roundingIncrementFeet;
                
                // Enforce minimum width
                if (roundedWidth < minWidthFeet) roundedWidth = minWidthFeet;
                
                // Apply if different
                double currentWidth = widthParam.AsDouble();
                if (Math.Abs(currentWidth - roundedWidth) > 0.01) {
                    try {
                        widthParam.Set(roundedWidth);
                        resized++;
                    } catch (Exception ex) {
                        sb.AppendLine($"Failed to resize duct {duct.Id}: {ex.Message}");
                    }
                }
            }
        }
    }
    t.Commit();
    sb.AppendLine($"Successfully resized {resized} duct segments (including branches and verticals) based on 1000 FPM target velocity, keeping height constant, and rounding to nearest 50mm.");
}

return sb.ToString();
