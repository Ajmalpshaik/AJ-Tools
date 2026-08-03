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

if (!terminals.Any() || !ducts.Any()) {
    sb.AppendLine("Missing terminals or ducts.");
    return sb.ToString();
}

int successCount = 0;
int failCount = 0;

using (var t = new Transaction(Document, "AJ Tools - Connect All Terminals"))
{
    t.Start();
    foreach (var terminal in terminals) {
        try {
            var connector = terminal.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().FirstOrDefault(c => c.Domain == Domain.DomainHvac);
            if (connector == null) {
                failCount++;
                continue;
            }

            var startPt = connector.Origin;
            
            MEPCurve nearestDuct = null;
            XYZ endPt = null;
            double minDist = double.MaxValue;

            foreach (var duct in ducts) {
                if (duct.Location is LocationCurve lc && lc.Curve is Line ductLine) {
                    var proj = ductLine.Project(startPt);
                    if (proj != null) {
                        if (proj.Distance < minDist) {
                            minDist = proj.Distance;
                            nearestDuct = duct;
                            endPt = proj.XYZPoint;
                        }
                    }
                }
            }

            if (nearestDuct != null && endPt != null) {
                if (startPt.DistanceTo(endPt) > 0.1) {
                    var sysTypeId = nearestDuct.MEPSystem?.GetTypeId() ?? nearestDuct.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId();
                    var ductTypeId = nearestDuct.GetTypeId();
                    var levelId = nearestDuct.ReferenceLevel?.Id ?? terminal.LevelId;

                    var newDuct = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysTypeId, ductTypeId, levelId, startPt, endPt);
                    
                    var c1 = newDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(startPt)).First();
                    c1.ConnectTo(connector);
                    
                    var c2 = newDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(endPt)).First();
                    Document.Create.NewTakeoffFitting(c2, nearestDuct);
                    
                    successCount++;
                } else {
                    failCount++;
                }
            } else {
                failCount++;
            }
        } catch (Exception ex) {
            sb.AppendLine($"Terminal {terminal.Id} failed: {ex.Message}");
            failCount++;
        }
    }
    t.Commit();
    sb.AppendLine($"Successfully routed {successCount} terminals. Failed {failCount}.");
}

return sb.ToString();
