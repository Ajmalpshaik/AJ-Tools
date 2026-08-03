var sb = new System.Text.StringBuilder();

int resized = 0;
double targetVelocityFPS = 1000.0 / 60.0;
double roundingIncrementFeet = 50.0 / 304.8;
double minWidthFeet = 150.0 / 304.8;

using (var t = new Transaction(Document, "AJ Tools - Resize All Elements"))
{
    t.Start();
    
    // 1. Resize Ducts
    var ducts = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_DuctCurves)
        .WhereElementIsNotElementType()
        .Cast<MEPCurve>()
        .ToList();
        
    foreach (var duct in ducts) {
        var flowParam = duct.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
        var heightParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
        var widthParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
        
        if (flowParam != null && heightParam != null && widthParam != null) {
            double flow = flowParam.AsDouble();
            double height = heightParam.AsDouble();
            if (flow > 0 && height > 0) {
                double reqW = (flow / targetVelocityFPS) / height;
                double roundedW = Math.Ceiling(reqW / roundingIncrementFeet) * roundingIncrementFeet;
                if (roundedW < minWidthFeet) roundedW = minWidthFeet;
                if (Math.Abs(widthParam.AsDouble() - roundedW) > 0.01) {
                    try { widthParam.Set(roundedW); resized++; } catch {}
                }
            }
        }
    }

    // 2. Resize Fittings and Accessories
    var others = new FilteredElementCollector(Document)
        .WhereElementIsNotElementType()
        .WherePasses(new LogicalOrFilter(new ElementCategoryFilter(BuiltInCategory.OST_DuctFitting), new ElementCategoryFilter(BuiltInCategory.OST_DuctAccessory)))
        .Cast<FamilyInstance>()
        .ToList();
    
    foreach (var inst in others) {
        // Find maximum Flow across connectors
        double flow = 0;
        if (inst.MEPModel != null && inst.MEPModel.ConnectorManager != null) {
            foreach (Connector c in inst.MEPModel.ConnectorManager.Connectors) {
                if (c.Flow > flow) flow = c.Flow;
            }
        }
        
        // Find Height
        double height = 0;
        Parameter heightParam = inst.LookupParameter("Duct Height") ?? inst.LookupParameter("Height");
        if (heightParam != null) {
            height = heightParam.AsDouble();
        } else if (inst.MEPModel != null && inst.MEPModel.ConnectorManager != null) {
            foreach (Connector c in inst.MEPModel.ConnectorManager.Connectors) {
                if (c.Shape == ConnectorProfileType.Rectangular && c.Height > height) height = c.Height;
            }
        }
        
        if (flow > 0 && height > 0) {
            double reqW = (flow / targetVelocityFPS) / height;
            double roundedW = Math.Ceiling(reqW / roundingIncrementFeet) * roundingIncrementFeet;
            if (roundedW < minWidthFeet) roundedW = minWidthFeet;
            
            // Try to set any writable Width parameter
            string[] wNames = new string[] { "Duct Width", "Width" };
            foreach (var n in wNames) {
                var p = inst.LookupParameter(n);
                if (p != null && !p.IsReadOnly) {
                    if (Math.Abs(p.AsDouble() - roundedW) > 0.01) {
                        try { p.Set(roundedW); resized++; } catch {}
                    }
                }
            }
        }
    }
    
    t.Commit();
    sb.AppendLine($"Successfully calculated and resized {resized} total elements (Ducts, Fittings, Accessories) based on 1000 FPM velocity.");
}
return sb.ToString();
