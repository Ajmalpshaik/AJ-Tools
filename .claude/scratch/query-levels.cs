var sb = new System.Text.StringBuilder();

var levels = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .OrderBy(l => l.Elevation)
    .ToList();

foreach (var l in levels) {
    sb.AppendLine($"Level: {l.Name} | Elevation: {l.Elevation:F2} ft | Id: {l.Id}");
}

return sb.ToString();
