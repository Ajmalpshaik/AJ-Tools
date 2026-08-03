var sb = new System.Text.StringBuilder();

var terminals = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctTerminal)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .ToList();

var ducts = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctCurves)
    .WhereElementIsNotElementType()
    .Cast<MEPCurve>()
    .ToList();

// Identify main horizontal ducts
var mainDucts = ducts.Where(d => {
    var lc = d.Location as LocationCurve;
    var p1 = lc.Curve.GetEndPoint(0);
    var p2 = lc.Curve.GetEndPoint(1);
    return Math.Abs(p1.Z - p2.Z) < 0.01;
}).ToList();

if (!mainDucts.Any()) {
    sb.AppendLine("No main horizontal ducts found.");
    return sb.ToString();
}

using (var t = new Transaction(Document, "AJ Tools - L-Shape Connect"))
{
    t.Start();
    
    // 1. Delete bad diagonal ducts
    int delCount = 0;
    foreach(var d in ducts) {
        var lc = d.Location as LocationCurve;
        var p1 = lc.Curve.GetEndPoint(0);
        var p2 = lc.Curve.GetEndPoint(1);
        bool isHoriz = Math.Abs(p1.Z - p2.Z) < 0.01;
        bool isVert = Math.Abs(p1.X - p2.X) < 0.01 && Math.Abs(p1.Y - p2.Y) < 0.01;
        if (!isHoriz && !isVert) {
            try { Document.Delete(d.Id); delCount++; } catch {}
        }
    }
    sb.AppendLine($"Deleted {delCount} diagonal ducts.");

    // 2. Connect unconnected terminals
    int successCount = 0;
    foreach (var terminal in terminals) {
        var connector = terminal.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().FirstOrDefault(c => c.Domain == Domain.DomainHvac);
        if (connector == null) continue;
        
        // Skip if already fully connected (like the user's example!)
        if (connector.IsConnected && connector.AllRefs.Size > 1) continue;

        var tPt = connector.Origin;
        
        MEPCurve nearestMain = null;
        XYZ mainPt = null;
        double minDist = double.MaxValue;

        foreach (var md in mainDucts) {
            var lc = md.Location as LocationCurve;
            var proj = (lc.Curve as Line).Project(tPt);
            if (proj != null) {
                var dist = new XYZ(proj.XYZPoint.X, proj.XYZPoint.Y, 0).DistanceTo(new XYZ(tPt.X, tPt.Y, 0));
                if (dist < minDist) {
                    minDist = dist;
                    nearestMain = md;
                    mainPt = proj.XYZPoint;
                }
            }
        }

        if (nearestMain != null && mainPt != null) {
            try {
                var sysTypeId = nearestMain.MEPSystem?.GetTypeId() ?? nearestMain.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId();
                var ductTypeId = nearestMain.GetTypeId();
                var levelId = nearestMain.ReferenceLevel?.Id ?? terminal.LevelId;

                XYZ branchEnd = new XYZ(tPt.X, tPt.Y, mainPt.Z);
                
                MEPCurve horizDuct = null;
                if (mainPt.DistanceTo(branchEnd) > 0.5) {
                    horizDuct = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysTypeId, ductTypeId, levelId, mainPt, branchEnd);
                } else {
                    branchEnd = mainPt;
                }

                MEPCurve vertDuct = null;
                if (branchEnd.DistanceTo(tPt) > 0.5) {
                    vertDuct = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysTypeId, ductTypeId, levelId, branchEnd, tPt);
                }

                if (vertDuct != null) {
                    var vertBottom = vertDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(tPt)).First();
                    vertBottom.ConnectTo(connector);
                    
                    if (horizDuct != null) {
                        var vertTop = vertDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(branchEnd)).First();
                        var horizEnd = horizDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(branchEnd)).First();
                        vertTop.ConnectTo(horizEnd);
                        Document.Create.NewElbowFitting(vertTop, horizEnd);
                        
                        var horizStart = horizDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(mainPt)).First();
                        Document.Create.NewTakeoffFitting(horizStart, nearestMain);
                    } else {
                        var vertTop = vertDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(branchEnd)).First();
                        Document.Create.NewTakeoffFitting(vertTop, nearestMain);
                    }
                } else if (horizDuct != null) {
                    var horizEnd = horizDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(branchEnd)).First();
                    horizEnd.ConnectTo(connector);
                    
                    var horizStart = horizDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(mainPt)).First();
                    Document.Create.NewTakeoffFitting(horizStart, nearestMain);
                }
                
                successCount++;
            } catch (Exception ex) {
                sb.AppendLine($"Error routing terminal {terminal.Id}: {ex.Message}");
            }
        }
    }
    t.Commit();
    sb.AppendLine($"Routed {successCount} terminals perfectly.");
}
return sb.ToString();
