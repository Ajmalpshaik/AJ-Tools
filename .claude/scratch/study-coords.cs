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
    sb.AppendLine($"Duct ID: {sampleDuct.Id}");
    var line = (sampleDuct.Location as LocationCurve).Curve as Line;
    
    XYZ p0 = line.GetEndPoint(0);
    XYZ p1 = line.GetEndPoint(1);
    
    sb.AppendLine($"P0: {p0.X:F1}, {p0.Y:F1}, {p0.Z:F1}");
    sb.AppendLine($"P1: {p1.X:F1}, {p1.Y:F1}, {p1.Z:F1}");
    
    XYZ pUpConn = p0;
    var inConn = sampleDuct.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.End && c.Direction == FlowDirectionType.In);
    if (inConn != null) {
        pUpConn = inConn.Origin;
        sb.AppendLine($"In Connector found at: {inConn.Origin.X:F1}, {inConn.Origin.Y:F1}, {inConn.Origin.Z:F1}");
    }
    
    bool isP0Upstream = pUpConn.DistanceTo(p0) < pUpConn.DistanceTo(p1);
    sb.AppendLine($"isP0Upstream: {isP0Upstream}");
    
    var takeoffs = new List<double>();
    foreach (Connector c in sampleDuct.ConnectorManager.Connectors) {
        if (c.ConnectorType == ConnectorType.Curve && c.IsConnected) {
            foreach (Connector r in c.AllRefs) {
                if (r.Owner.Id != sampleDuct.Id && r.Owner is FamilyInstance fi && fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting) {
                    takeoffs.Add(r.Origin.X);
                }
            }
        }
    }
    takeoffs.Sort();
    sb.AppendLine("Takeoff X coordinates:");
    foreach(var x in takeoffs) sb.AppendLine($"  {x:F1}");
}

return sb.ToString();
