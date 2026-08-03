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
int totalUnions = 0;

using (var t = new Transaction(Document, "AJ Tools - Split 200mm Flow Direction"))
{
    t.Start();
    foreach (var duct in mainDucts) {
        var lc = duct.Location as LocationCurve;
        var line = lc.Curve as Line;
        var p0 = line.GetEndPoint(0);
        var p1 = line.GetEndPoint(1);
        double ductLength = line.Length;
        
        // Determine flow direction (upstream to downstream)
        XYZ pUp = p0;
        XYZ pDown = p1;
        var inConn = duct.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.End && c.Direction == FlowDirectionType.In);
        if (inConn != null) {
            pUp = inConn.Origin;
            pDown = p0.DistanceTo(pUp) < p1.DistanceTo(pUp) ? p1 : p0;
        } else {
            var c0 = duct.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.End && c.Origin.DistanceTo(p0) < 0.1);
            var c1 = duct.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.End && c.Origin.DistanceTo(p1) < 0.1);
            if (c1 != null && c0 != null && c1.Flow > c0.Flow + 0.1) {
                pUp = p1;
                pDown = p0;
            }
        }
        
        var dir = (pDown - pUp).Normalize();
        
        var takeoffDistances = new List<double>();
        foreach (Connector c in duct.ConnectorManager.Connectors) {
            if (c.ConnectorType == ConnectorType.Curve) {
                if (c.IsConnected) {
                    foreach (Connector r in c.AllRefs) {
                        if (r.Owner.Id != duct.Id && r.Owner is FamilyInstance fi && fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting) {
                            var proj = line.Project(r.Origin);
                            if (proj != null) takeoffDistances.Add(pUp.DistanceTo(proj.XYZPoint));
                        }
                    }
                }
            }
        }
        
        if (takeoffDistances.Count == 0) continue;
        
        takeoffDistances.Sort();
        
        var clusters = new List<List<double>>();
        var currentCluster = new List<double> { takeoffDistances[0] };
        
        // Group nearby takeoffs
        for (int i = 1; i < takeoffDistances.Count; i++) {
            if (takeoffDistances[i] - currentCluster.Last() < 2.0) {
                currentCluster.Add(takeoffDistances[i]);
            } else {
                clusters.Add(currentCluster);
                currentCluster = new List<double> { takeoffDistances[i] };
            }
        }
        clusters.Add(currentCluster);
        
        var breakDistances = new List<double>();
        double offsetInFeet = 200.0 / 304.8; // 200 mm downstream
        
        foreach (var cluster in clusters) {
            var downstreamMostTakeoff = cluster.Max();
            var splitDist = downstreamMostTakeoff + offsetInFeet;
            
            // Avoid hitting the next cluster
            var nextCluster = clusters.FirstOrDefault(c => c.Min() > downstreamMostTakeoff);
            if (nextCluster != null && splitDist > nextCluster.Min() - 0.2) {
                splitDist = (downstreamMostTakeoff + nextCluster.Min()) / 2.0;
            }
            
            if (splitDist < ductLength - 0.2) {
                breakDistances.Add(splitDist);
            }
        }
        
        var uniqueBreaks = breakDistances.Distinct().OrderByDescending(d => d).ToList();
        
        foreach (var dist in uniqueBreaks) {
            if (dist > 0.2 && dist < ductLength - 0.2) {
                var breakPt = pUp + dir * dist;
                try {
                    var newDuctId = Autodesk.Revit.DB.Mechanical.MechanicalUtils.BreakCurve(Document, duct.Id, breakPt);
                    totalSplits++;
                    
                    var newDuct = Document.GetElement(newDuctId) as MEPCurve;
                    var c1 = duct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
                    var c2 = newDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
                    
                    if (c1.IsConnectedTo(c2)) c1.DisconnectFrom(c2);
                    
                    var union = Document.Create.NewUnionFitting(c1, c2);
                    if (union != null) totalUnions++;
                    
                } catch (Exception ex) {
                    sb.AppendLine($"Failed at dist {dist:F1} on duct {duct.Id}: {ex.Message}");
                }
            }
        }
    }
    t.Commit();
    sb.AppendLine($"Successfully split main ducts at {totalSplits} locations and added {totalUnions} union fittings.");
}

return sb.ToString();
