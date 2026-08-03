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
    sb.AppendLine($"System Type: {sampleDuct.MEPSystem?.Name}");
    
    var line = (sampleDuct.Location as LocationCurve).Curve as Line;
    var p0 = line.GetEndPoint(0);
    var p1 = line.GetEndPoint(1);
    
    foreach (Connector c in sampleDuct.ConnectorManager.Connectors) {
        if (c.ConnectorType == ConnectorType.End) {
            string end = c.Origin.DistanceTo(p0) < 0.1 ? "P0" : (c.Origin.DistanceTo(p1) < 0.1 ? "P1" : "UNKNOWN");
            sb.AppendLine($"End Connector at {end}: FlowDirection={c.Direction}, Flow={c.Flow}");
        }
    }
}

return sb.ToString();
