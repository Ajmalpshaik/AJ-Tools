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

sb.AppendLine($"Found {terminals.Count} air terminals and {ducts.Count} ducts.");

// Test if MechanicalUtils exists
var type = typeof(Autodesk.Revit.DB.Mechanical.MechanicalUtils);
var methods = type.GetMethods();
bool hasConnect = methods.Any(m => m.Name.Contains("ConnectAirTerminalOnDuct"));
sb.AppendLine($"Has ConnectAirTerminalOnDuct: {hasConnect}");

return sb.ToString();
