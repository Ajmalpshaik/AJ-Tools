var sb = new System.Text.StringBuilder();

var level = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .OrderBy(l => l.Elevation)
    .FirstOrDefault();
if (level != null) sb.AppendLine($"Level: {level.Name} (Id: {level.Id})");

var wallType = new FilteredElementCollector(Document)
    .OfClass(typeof(WallType))
    .Cast<WallType>()
    .Where(wt => wt.Kind == WallKind.Basic)
    .FirstOrDefault();
if (wallType != null) sb.AppendLine($"WallType: {wallType.Name} (Id: {wallType.Id})");

return sb.ToString();
