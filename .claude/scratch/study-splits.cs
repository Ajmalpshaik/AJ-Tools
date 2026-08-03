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

var sampleDuct = mainDucts.FirstOrDefault(d => 
    d.ConnectorManager.Connectors.Cast<Connector>().Any(c => c.ConnectorType == ConnectorType.End && c.IsConnected && 
        c.AllRefs.Cast<Connector>().Any(r => r.Owner is FamilyInstance fi && fi.Name.Contains("Union")))
    && d.ConnectorManager.Connectors.Cast<Connector>().Any(c => c.ConnectorType == ConnectorType.Curve && c.IsConnected)
);

if (sampleDuct != null) {
    sb.AppendLine($"Duct ID: {sampleDuct.Id}");
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
    
    sb.AppendLine($"Upstream end is {(isP0Upstream ? "P0" : "P1")}");
    
    var takeoffs = new List<(double dist, double halfW, string type)>();
    foreach (Connector c in sampleDuct.ConnectorManager.Connectors) {
        if (c.ConnectorType == ConnectorType.Curve && c.IsConnected) {
            foreach (Connector r in c.AllRefs) {
                if (r.Owner.Id != sampleDuct.Id && r.Owner is FamilyInstance fi && fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting) {
                    var proj = line.Project(r.Origin);
                    if (proj != null) {
                        double hw = r.Shape == ConnectorProfileType.Rectangular ? r.Width / 2.0 : r.Radius;
                        takeoffs.Add((actualUpstream.DistanceTo(proj.XYZPoint), hw, "Takeoff"));
                    }
                }
            }
        } else if (c.ConnectorType == ConnectorType.End && c.IsConnected) {
             foreach (Connector r in c.AllRefs) {
                 if (r.Owner is FamilyInstance fi && fi.Name.Contains("Union")) {
                      var proj = line.Project(r.Origin);
                      if (proj != null) {
                          takeoffs.Add((actualUpstream.DistanceTo(proj.XYZPoint), 0, "Union"));
                      }
                 }
             }
        }
    }
    
    foreach(var t in takeoffs.OrderBy(t => t.dist)) {
        sb.AppendLine($"{t.type} at {t.dist * 304.8:F1} mm. (HalfWidth: {t.halfW * 304.8:F1} mm)");
    }
} else {
    sb.AppendLine("No suitable duct found.");
}

return sb.ToString();
