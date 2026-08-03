var sb = new System.Text.StringBuilder();

var doorType = new FilteredElementCollector(Document)
    .OfClass(typeof(FamilySymbol))
    .OfCategory(BuiltInCategory.OST_Doors)
    .Cast<FamilySymbol>()
    .FirstOrDefault(t => t.IsActive || t.Name != null);

if (doorType != null) sb.AppendLine($"Door: {doorType.FamilyName} - {doorType.Name} (Id: {doorType.Id})");
else sb.AppendLine("No Door type found.");

var windowType = new FilteredElementCollector(Document)
    .OfClass(typeof(FamilySymbol))
    .OfCategory(BuiltInCategory.OST_Windows)
    .Cast<FamilySymbol>()
    .FirstOrDefault(t => t.IsActive || t.Name != null);

if (windowType != null) sb.AppendLine($"Window: {windowType.FamilyName} - {windowType.Name} (Id: {windowType.Id})");
else sb.AppendLine("No Window type found.");

var floorType = new FilteredElementCollector(Document)
    .OfClass(typeof(FloorType))
    .Cast<FloorType>()
    .FirstOrDefault();

if (floorType != null) sb.AppendLine($"FloorType: {floorType.Name} (Id: {floorType.Id})");
else sb.AppendLine("No Floor type found.");

return sb.ToString();
