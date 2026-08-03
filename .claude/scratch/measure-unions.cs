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
);

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
    
    // Find all connected elements along this duct run (follow unions)
    // Actually, just find ALL takeoffs and ALL unions in the whole project, and find their Y coordinates.
    // Since they are all on the same main duct line (X = -247.8), we can just query them!
}

var allTakeoffs = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctFitting).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(f => !f.Name.Contains("Union") && !f.Name.Contains("Cap") && !f.Name.Contains("Elbow")).ToList();
var allUnions = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctFitting).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(f => f.Name.Contains("Union")).ToList();

var takeoffY = allTakeoffs.Select(t => (t.Location as LocationPoint).Point.Y).OrderBy(y => y).ToList();
var unionY = allUnions.Select(u => (u.Location as LocationPoint).Point.Y).OrderBy(y => y).ToList();

sb.AppendLine("Takeoff Y Coordinates:");
foreach(var y in takeoffY) sb.AppendLine($"  {y:F1}");

sb.AppendLine("Union Y Coordinates:");
foreach(var y in unionY) sb.AppendLine($"  {y:F1}");

return sb.ToString();
