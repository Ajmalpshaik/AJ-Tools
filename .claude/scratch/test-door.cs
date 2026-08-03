var sb = new System.Text.StringBuilder();

var level = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .OrderBy(l => l.Elevation)
    .FirstOrDefault();

var doorType = new FilteredElementCollector(Document)
    .OfClass(typeof(FamilySymbol))
    .OfCategory(BuiltInCategory.OST_Doors)
    .Cast<FamilySymbol>()
    .FirstOrDefault();

var walls = new FilteredElementCollector(Document)
    .OfClass(typeof(Wall))
    .WhereElementIsNotElementType()
    .Cast<Wall>()
    .Where(w => {
        if(w.Location is LocationCurve lc) return lc.Curve.GetEndPoint(0).X >= 200;
        return false;
    })
    .ToList();

if (walls.Count == 0 || doorType == null) {
    sb.AppendLine("No walls or door type found.");
    return sb.ToString();
}

var testWall = walls.First();

using (var t = new Transaction(Document, "AJ Tools - Test Door"))
{
    t.Start();
    try
    {
        if (!doorType.IsActive) doorType.Activate();
        
        var lc = testWall.Location as LocationCurve;
        var pt = lc.Curve.Evaluate(0.5, true); // Midpoint
        
        var door = Document.Create.NewFamilyInstance(pt, doorType, testWall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
        if (door != null) {
            sb.AppendLine($"Door created with ID {door.Id} at {pt}");
        } else {
            sb.AppendLine("Door returned null.");
        }
        t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Exception: {ex.Message}");
    }
}

return sb.ToString();
