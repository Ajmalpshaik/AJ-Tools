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

sb.AppendLine($"Main ducts found: {mainDucts.Count}");

foreach (var duct in mainDucts) {
    var lc = duct.Location as LocationCurve;
    var line = lc.Curve as Line;
    var p0 = line.GetEndPoint(0);
    
    var takeoffDistances = new List<double>();
    foreach (Connector c in duct.ConnectorManager.Connectors) {
        if (c.IsConnected) {
            foreach (Connector r in c.AllRefs) {
                if (r.Owner.Id != duct.Id && r.Owner is FamilyInstance fi) {
                    var ptp = fi.Symbol.Family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
                    if (ptp != null) {
                        int pt = ptp.AsInteger();
                        if (pt == 23 || pt == 24 || pt == 25 || pt == 28) {
                            var proj = line.Project(c.Origin);
                            if (proj != null) {
                                takeoffDistances.Add(p0.DistanceTo(proj.XYZPoint));
                            }
                        }
                    }
                }
            }
        }
    }
    
    if (takeoffDistances.Count > 0) {
        sb.AppendLine($"Duct {duct.Id} has {takeoffDistances.Count} takeoffs.");
        takeoffDistances.Sort();
        sb.AppendLine($"  Distances: {string.Join(", ", takeoffDistances.Select(d => d.ToString("F1")))}");
        
        var clusters = new List<List<double>>();
        var currentCluster = new List<double> { takeoffDistances[0] };
        
        for (int i = 1; i < takeoffDistances.Count; i++) {
            if (takeoffDistances[i] - currentCluster.Last() < 2.5) {
                currentCluster.Add(takeoffDistances[i]);
            } else {
                clusters.Add(currentCluster);
                currentCluster = new List<double> { takeoffDistances[i] };
            }
        }
        clusters.Add(currentCluster);
        sb.AppendLine($"  Clusters: {clusters.Count}");
    }
}

return sb.ToString();
