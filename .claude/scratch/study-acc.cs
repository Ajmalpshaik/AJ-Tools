var sb = new System.Text.StringBuilder();

var accessories = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctAccessory)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Take(2)
    .ToList();

var fittings = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctFitting)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Take(2)
    .ToList();

sb.AppendLine("ACCESSORIES:");
foreach (var a in accessories) {
    sb.AppendLine($"Name: {a.Name}");
    foreach (Parameter p in a.Parameters) {
        if (p.Definition.Name.ToLower().Contains("width") || p.Definition.Name.ToLower().Contains("height")) {
            sb.AppendLine($"  {p.Definition.Name}: {p.AsValueString()} (IsReadOnly: {p.IsReadOnly})");
        }
    }
}
sb.AppendLine("FITTINGS:");
foreach (var f in fittings) {
    sb.AppendLine($"Name: {f.Name}");
    foreach (Parameter p in f.Parameters) {
        if (p.Definition.Name.ToLower().Contains("width") || p.Definition.Name.ToLower().Contains("height")) {
            sb.AppendLine($"  {p.Definition.Name}: {p.AsValueString()} (IsReadOnly: {p.IsReadOnly})");
        }
    }
}
return sb.ToString();
