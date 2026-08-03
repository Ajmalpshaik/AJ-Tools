var sb = new System.Text.StringBuilder();

var ducts = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctCurves)
    .WhereElementIsNotElementType()
    .Cast<MEPCurve>()
    .ToList();

var mainDucts = ducts.Where(d => {
    if (d.Location is LocationCurve lc && lc.Curve is Line line) {
        return Math.Abs(line.GetEndPoint(0).Z - line.GetEndPoint(1).Z) < 0.01;
    }
    return false;
}).ToList();

var sampleDuct = mainDucts.FirstOrDefault(d => d.ConnectorManager.Connectors.Cast<Connector>().Any(c => c.ConnectorType == ConnectorType.Curve && c.IsConnected));

if (sampleDuct != null) {
    var line = (sampleDuct.Location as LocationCurve).Curve as Line;
    
    XYZ pUpConn = line.GetEndPoint(0);
    var inConn = sampleDuct.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.End && c.Direction == FlowDirectionType.In);
    if (inConn != null) pUpConn = inConn.Origin;
    else {
        var c0 = sampleDuct.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.End && c.Origin.DistanceTo(line.GetEndPoint(0)) < 0.1);
        var c1 = sampleDuct.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.End && c.Origin.DistanceTo(line.GetEndPoint(1)) < 0.1);
        if (c1 != null && c0 != null && c1.Flow > c0.Flow + 0.1) pUpConn = line.GetEndPoint(1);
    }
    
    bool isP0Upstream = pUpConn.DistanceTo(line.GetEndPoint(0)) < pUpConn.DistanceTo(line.GetEndPoint(1));
    XYZ actualUpstream = isP0Upstream ? line.GetEndPoint(0) : line.GetEndPoint(1);
    XYZ actualDownstream = isP0Upstream ? line.GetEndPoint(1) : line.GetEndPoint(0);
    var dir = (actualDownstream - actualUpstream).Normalize();
    
    sb.AppendLine($"Analyzing Duct: {sampleDuct.Id}");
    
    foreach (Connector c in sampleDuct.ConnectorManager.Connectors) {
        if (c.ConnectorType == ConnectorType.Curve && c.IsConnected) {
            foreach (Connector r in c.AllRefs) {
                if (r.Owner.Id != sampleDuct.Id && r.Owner is FamilyInstance fi && fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting) {
                    var proj = line.Project(r.Origin);
                    if (proj != null) {
                        double distFromStart = actualUpstream.DistanceTo(proj.XYZPoint);
                        
                        // Check the shape and size of the connector
                        string sizeInfo = "";
                        if (r.Shape == ConnectorProfileType.Rectangular) sizeInfo = $"Width: {r.Width * 304.8:F1}mm";
                        else if (r.Shape == ConnectorProfileType.Round) sizeInfo = $"Radius: {r.Radius * 304.8:F1}mm";
                        else sizeInfo = $"Shape: {r.Shape}";
                        
                        sb.AppendLine($"  Takeoff at distance: {distFromStart * 304.8:F1} mm | Connector Size: {sizeInfo}");
                    }
                }
            }
        }
    }
} else {
    sb.AppendLine("No main duct with connected takeoffs found.");
}

return sb.ToString();
