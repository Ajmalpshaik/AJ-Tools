var sb = new System.Text.StringBuilder();

var fittings = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctFitting)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .ToList();

var groups = fittings.GroupBy(f => f.Name).ToList();

foreach (var group in groups) {
    var f = group.First();
    sb.AppendLine($"Fitting Name: {f.Name}");
    foreach (Parameter p in f.Parameters) {
        if (p.Definition.Name.ToLower().Contains("width") || p.Definition.Name.ToLower().Contains("height")) {
            sb.AppendLine($"  {p.Definition.Name}: {p.AsValueString()} (IsReadOnly: {p.IsReadOnly})");
        }
    }
}

return sb.ToString();
