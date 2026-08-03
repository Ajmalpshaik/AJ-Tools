var sb = new System.Text.StringBuilder();

var terminals = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctTerminal)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .ToList();

var ducts = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctCurves)
    .WhereElementIsNotElementType()
    .ToList();

if (terminals.Count == 0) {
    sb.AppendLine("No air terminals found in the model.");
    return sb.ToString();
}

if (ducts.Count == 0) {
    sb.AppendLine("No ducts found in the model.");
    return sb.ToString();
}

int connectedCount = 0;
int failedCount = 0;

using (var t = new Transaction(Document, "AJ Tools - Connect Air Terminals"))
{
    t.Start();
    
    try
    {
        foreach (var terminal in terminals)
        {
            if (!(terminal.Location is LocationPoint lp)) continue;
            XYZ tPt = lp.Point;
            
            Element nearestDuct = null;
            double minDistance = double.MaxValue;
            
            foreach (var duct in ducts)
            {
                if (duct.Location is LocationCurve lc)
                {
                    var proj = lc.Curve.Project(tPt);
                    if (proj != null)
                    {
                        if (proj.Distance < minDistance)
                        {
                            minDistance = proj.Distance;
                            nearestDuct = duct;
                        }
                    }
                    else 
                    {
                        var d1 = tPt.DistanceTo(lc.Curve.GetEndPoint(0));
                        var d2 = tPt.DistanceTo(lc.Curve.GetEndPoint(1));
                        var minD = Math.Min(d1, d2);
                        if (minD < minDistance)
                        {
                            minDistance = minD;
                            nearestDuct = duct;
                        }
                    }
                }
            }
            
            if (nearestDuct != null)
            {
                try {
                    Autodesk.Revit.DB.Mechanical.MechanicalUtils.ConnectAirTerminalOnDuct(Document, nearestDuct.Id, terminal.Id);
                    connectedCount++;
                } catch (Exception ex) {
                    failedCount++;
                    sb.AppendLine($"Failed to connect terminal {terminal.Id}: {ex.Message}");
                }
            }
        }
        t.Commit();
        sb.AppendLine($"Successfully connected {connectedCount} air terminals to the nearest duct.");
        if (failedCount > 0) sb.AppendLine($"{failedCount} terminals failed to connect due to geometric constraints.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Transaction error: {ex.Message}");
    }
}

return sb.ToString();
