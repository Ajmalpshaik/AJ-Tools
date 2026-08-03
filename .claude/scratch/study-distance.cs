var sb = new System.Text.StringBuilder();

var unions = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctFitting)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Where(f => f.Name.Contains("Union"))
    .ToList();

var takeoffs = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctFitting)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Where(f => !f.Name.Contains("Union") && !f.Name.Contains("Cap") && !f.Name.Contains("Elbow"))
    .ToList();

sb.AppendLine($"Found {unions.Count} unions and {takeoffs.Count} takeoffs.");

if (unions.Count > 0 && takeoffs.Count > 0) {
    var u = unions.First();
    var uLoc = (u.Location as LocationPoint).Point;
    
    // Find closest takeoff
    var t = takeoffs.OrderBy(x => (x.Location as LocationPoint).Point.DistanceTo(uLoc)).First();
    var tLoc = (t.Location as LocationPoint).Point;
    
    double centerDist = uLoc.DistanceTo(tLoc);
    sb.AppendLine($"Distance from Union Center to Takeoff Center: {centerDist * 304.8:F1} mm");
    
    // Get takeoff connectors to main duct
    var mainConn = t.MEPModel.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.Curve);
    if (mainConn != null) {
        double hw = mainConn.Shape == ConnectorProfileType.Rectangular ? mainConn.Width / 2.0 : mainConn.Radius;
        sb.AppendLine($"Takeoff main connector half-width: {hw * 304.8:F1} mm");
        
        double edgeDist = centerDist - hw;
        sb.AppendLine($"Calculated distance from Takeoff Edge to Union Center: {edgeDist * 304.8:F1} mm");
        
        // Also account for union width
        var uConn = u.MEPModel.ConnectorManager.Connectors.Cast<Connector>().First();
        double uLength = 0;
        // The distance between the two connectors of the union is its length
        var uConns = u.MEPModel.ConnectorManager.Connectors.Cast<Connector>().ToList();
        if (uConns.Count == 2) {
            uLength = uConns[0].Origin.DistanceTo(uConns[1].Origin);
            sb.AppendLine($"Union length (between connectors): {uLength * 304.8:F1} mm");
        }
        
        double gap = centerDist - hw - (uLength / 2.0);
        sb.AppendLine($"TRUE VISUAL GAP (Edge to Edge): {gap * 304.8:F1} mm");
    }
}

return sb.ToString();
