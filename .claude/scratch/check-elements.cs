var sb = new System.Text.StringBuilder();

var doors = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().ToElements();
var windows = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().ToElements();
var floors = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType().ToElements();

int dCount = 0, wCount = 0, fCount = 0;
foreach(var e in doors) { if (e.Location is LocationPoint lp && lp.Point.X > 150) dCount++; }
foreach(var e in windows) { if (e.Location is LocationPoint lp && lp.Point.X > 150) wCount++; }
foreach(var e in floors) { 
    // Just count floors created recently or generally
    fCount++; 
}

sb.AppendLine($"Found {dCount} doors, {wCount} windows, {fCount} floors.");
return sb.ToString();
