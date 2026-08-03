var sb = new System.Text.StringBuilder();

var types = typeof(Autodesk.Revit.DB.Mechanical.Duct).Assembly.GetTypes();
var sizingTypes = types.Where(t => t.Name.Contains("Sizing") || t.Name.Contains("Size")).ToList();

sb.AppendLine("Duct Sizing APIs:");
foreach(var t in sizingTypes) {
    sb.AppendLine($"Type: {t.Name}");
    foreach(var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) {
        sb.AppendLine($"  Method: {m.Name}");
    }
}

return sb.ToString();
