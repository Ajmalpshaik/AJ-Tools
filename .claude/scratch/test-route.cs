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

var terminal = terminals.First();
var mainDuct = ducts.First();

var connector = terminal.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>().FirstOrDefault(c => c.Domain == Domain.DomainHvac);
if (connector == null) {
    sb.AppendLine("Terminal has no HVAC connector.");
    return sb.ToString();
}

var lc = mainDuct.Location as LocationCurve;
var ductLine = lc.Curve as Line;
var startPt = connector.Origin;
var proj = ductLine.Project(startPt);
var endPt = proj.XYZPoint;

// Let's create a branch duct
var sysTypeId = mainDuct.MEPSystem?.GetTypeId() ?? mainDuct.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId();
var ductTypeId = mainDuct.GetTypeId();
var levelId = mainDuct.ReferenceLevel?.Id ?? terminal.LevelId;

using (var t = new Transaction(Document, "Test Connect 1 Terminal"))
{
    t.Start();
    try
    {
        // To avoid creating a duct of length 0 if they are exactly aligned
        if (startPt.DistanceTo(endPt) > 0.1) {
            var newDuct = Duct.Create(Document, sysTypeId, ductTypeId, levelId, startPt, endPt);
            
            // Connect to terminal
            var c1 = newDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(startPt)).First();
            c1.ConnectTo(connector);
            
            // Connect to main duct
            var c2 = newDuct.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(endPt)).First();
            Document.Create.NewTakeoffFitting(c2, mainDuct);
            
            sb.AppendLine("Successfully created branch duct and connected to terminal and main duct.");
        } else {
            sb.AppendLine("Terminal is exactly on the duct.");
        }
        
        t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Error: {ex.Message}");
    }
}

return sb.ToString();
