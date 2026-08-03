var sb = new System.Text.StringBuilder();

var fittings = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctFitting)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Where(f => f.Name.Contains("Union") || f.Symbol.Family.Name.Contains("Union"))
    .ToList();

sb.AppendLine($"Found {fittings.Count} Union fittings.");

foreach (var union in fittings) {
    sb.AppendLine($"Union ID: {union.Id}");
    var cm = union.MEPModel?.ConnectorManager;
    if (cm == null) continue;
    
    var connectedDucts = new List<MEPCurve>();
    foreach (Connector c in cm.Connectors) {
        if (c.IsConnected) {
            foreach (Connector r in c.AllRefs) {
                if (r.Owner.Id != union.Id && r.Owner is MEPCurve curve) {
                    connectedDucts.Add(curve);
                }
            }
        }
    }
    
    foreach (var duct in connectedDucts) {
        var lc = duct.Location as LocationCurve;
        if (lc != null && lc.Curve != null) {
            sb.AppendLine($"  Connected to Duct ID: {duct.Id}");
            sb.AppendLine($"    P0: {lc.Curve.GetEndPoint(0)}, P1: {lc.Curve.GetEndPoint(1)}");
            
            // Find takeoffs on this duct
            int takeoffCount = 0;
            foreach (Connector c in duct.ConnectorManager.Connectors) {
                if (c.ConnectorType == ConnectorType.Curve) {
                    if (c.IsConnected) {
                        foreach (Connector r in c.AllRefs) {
                            if (r.Owner.Id != duct.Id && r.Owner is FamilyInstance fi && fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting) {
                                var proj = (lc.Curve as Line)?.Project(r.Origin);
                                if (proj != null) {
                                    sb.AppendLine($"    Has Takeoff ID: {fi.Id} at {proj.XYZPoint}");
                                    takeoffCount++;
                                }
                            }
                        }
                    }
                }
            }
            if (takeoffCount == 0) {
                sb.AppendLine("    No takeoffs on this duct.");
            }
        }
    }
}

return sb.ToString();
