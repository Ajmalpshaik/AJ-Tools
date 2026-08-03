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

int totalSplits = 0;

using (var t = new Transaction(Document, "AJ Tools - Auto Split Ducts"))
{
    t.Start();
    foreach (var duct in mainDucts) {
        var lc = duct.Location as LocationCurve;
        var line = lc.Curve as Line;
        var p0 = line.GetEndPoint(0);
        var p1 = line.GetEndPoint(1);
        double ductLength = line.Length;
        
        var takeoffDistances = new List<double>();
        foreach (Connector c in duct.ConnectorManager.Connectors) {
            if (c.ConnectorType == ConnectorType.Curve) {
                if (c.IsConnected) {
                    foreach (Connector r in c.AllRefs) {
                        if (r.Owner.Id != duct.Id && r.Owner is FamilyInstance fi && fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting) {
                            var proj = line.Project(r.Origin);
                            if (proj != null) takeoffDistances.Add(p0.DistanceTo(proj.XYZPoint));
                        }
                    }
                }
            }
        }
        
        if (takeoffDistances.Count == 0) continue;
        
        takeoffDistances.Sort();
        
        // Cluster distances (if within 2.5 feet, consider them a group)
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
        
        var breakDistances = new List<double>();
        
        // Split midway between clusters
        for (int i = 0; i < clusters.Count - 1; i++) {
            var upstreamMax = clusters[i].Max();
            var downstreamMin = clusters[i+1].Min();
            var midDist = (upstreamMax + downstreamMin) / 2.0;
            
            if (downstreamMin - upstreamMax > 1.5) {
                breakDistances.Add(midDist);
            }
        }
        
        // If there's only 1 cluster, or for the last cluster, maybe split towards the long end
        // if there's enough space.
        var firstMin = clusters.First().Min();
        var lastMax = clusters.Last().Max();
        
        if (firstMin > 4.0) {
            breakDistances.Add(firstMin - 2.0); // Split before first cluster
        }
        
        if (ductLength - lastMax > 4.0) {
            breakDistances.Add(lastMax + 2.0); // Split after last cluster
        }
        
        // Remove duplicates and sort descending to break back-to-front
        var uniqueBreaks = breakDistances.Distinct().OrderByDescending(d => d).ToList();
        
        foreach (var dist in uniqueBreaks) {
            // Safety check
            if (dist > 1.0 && dist < ductLength - 1.0) {
                var breakPt = p0 + line.Direction * dist;
                try {
                    Autodesk.Revit.DB.Mechanical.MechanicalUtils.BreakCurve(Document, duct.Id, breakPt);
                    totalSplits++;
                } catch (Exception) {
                    // Ignore failures (e.g. not enough room for fitting)
                }
            }
        }
    }
    t.Commit();
    sb.AppendLine($"Successfully split main ducts at {totalSplits} locations.");
}

return sb.ToString();
