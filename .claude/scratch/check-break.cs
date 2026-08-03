var sb = new System.Text.StringBuilder();

var type = typeof(Autodesk.Revit.DB.Mechanical.MechanicalUtils);
var methods = type.GetMethods();
bool hasBreak = methods.Any(m => m.Name.Contains("BreakCurve"));
sb.AppendLine($"Has BreakCurve: {hasBreak}");

foreach (var m in methods.Where(m => m.Name.Contains("BreakCurve"))) {
    sb.AppendLine($"Method: {m.Name}");
    foreach (var p in m.GetParameters()) {
        sb.AppendLine($"  - {p.ParameterType.Name} {p.Name}");
    }
}

return sb.ToString();
