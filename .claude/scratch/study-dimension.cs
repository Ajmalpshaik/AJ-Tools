using Autodesk.Revit.DB;
using System.Linq;
using System.Collections.Generic;

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

if (unions.Count == 0) {
    return "No unions found. Did you undo the script?";
}

using (var t = new Transaction(Document, "AJ Tools - Add Debug Dimensions")) {
    t.Start();
    
    foreach (var u in unions) {
        var uLoc = (u.Location as LocationPoint).Point;
        
        var closestTakeoff = takeoffs.OrderBy(x => (x.Location as LocationPoint).Point.DistanceTo(uLoc)).FirstOrDefault();
        if (closestTakeoff != null) {
            double dist = (closestTakeoff.Location as LocationPoint).Point.DistanceTo(uLoc);
            if (dist < 10.0) {
                var tLoc = (closestTakeoff.Location as LocationPoint).Point;
                sb.AppendLine($"Union at {uLoc.X:F1},{uLoc.Y:F1} is {dist * 304.8:F1} mm (center-to-center) from Takeoff at {tLoc.X:F1},{tLoc.Y:F1}");
                
                var mainConn = closestTakeoff.MEPModel.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.ConnectorType == ConnectorType.Curve);
                if (mainConn != null) {
                    double hw = mainConn.Shape == ConnectorProfileType.Rectangular ? mainConn.Width / 2.0 : mainConn.Radius;
                    var uConns = u.MEPModel.ConnectorManager.Connectors.Cast<Connector>().ToList();
                    double uLength = 0;
                    if (uConns.Count == 2) uLength = uConns[0].Origin.DistanceTo(uConns[1].Origin);
                    
                    double gap = dist - hw - (uLength / 2.0);
                    sb.AppendLine($"  -> True edge-to-edge gap: {gap * 304.8:F1} mm");
                }
            }
        }
    }
    
    t.Commit();
}

return sb.ToString();
