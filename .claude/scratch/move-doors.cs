var sb = new System.Text.StringBuilder();

var doors = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_Doors)
    .WhereElementIsNotElementType()
    .ToElements();

var doorsToDelete = new List<ElementId>();
foreach (var d in doors) {
    if (d.Location is LocationPoint lp && lp.Point.X >= 299) {
        doorsToDelete.Add(d.Id);
    }
}

var level = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == "Level 1");
var doorType = new FilteredElementCollector(Document).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_Doors).Cast<FamilySymbol>().FirstOrDefault();

var walls = new FilteredElementCollector(Document).OfClass(typeof(Wall)).WhereElementIsNotElementType().Cast<Wall>().ToList();

Wall GetWall(double startX, double startY, double endX, double endY) {
    foreach(var w in walls) {
        if(w.Location is LocationCurve lc) {
            var p1 = lc.Curve.GetEndPoint(0);
            var p2 = lc.Curve.GetEndPoint(1);
            if ((Math.Abs(p1.X - startX) < 1 && Math.Abs(p1.Y - startY) < 1 && Math.Abs(p2.X - endX) < 1 && Math.Abs(p2.Y - endY) < 1) ||
                (Math.Abs(p2.X - startX) < 1 && Math.Abs(p2.Y - startY) < 1 && Math.Abs(p1.X - endX) < 1 && Math.Abs(p1.Y - endY) < 1)) {
                return w;
            }
        }
    }
    return null;
}

Wall wallBottom = GetWall(300, 0, 330, 0);
Wall wallVert1 = GetWall(316, 0, 316, 24);
Wall wallHorizLeft = GetWall(300, 14, 316, 14);

if (wallBottom == null || wallVert1 == null || wallHorizLeft == null || level == null || doorType == null) {
    sb.AppendLine("Failed to find walls, level or door type.");
    return sb.ToString();
}

using (var t = new Transaction(Document, "AJ Tools - Move Doors"))
{
    t.Start();
    try
    {
        foreach(var id in doorsToDelete) {
            try { Document.Delete(id); } catch { }
        }
        sb.AppendLine($"Deleted {doorsToDelete.Count} centered doors.");

        if (!doorType.IsActive) doorType.Activate();
        
        XYZ pt(double x, double y) => new XYZ(x, y, level.Elevation);
        
        // Front door (Living): pushed to left corner
        Document.Create.NewFamilyInstance(pt(302, 0), doorType, wallBottom, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
        
        // Master Bed: near bottom corner
        Document.Create.NewFamilyInstance(pt(316, 2), doorType, wallVert1, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
        
        // Guest Bed: near bottom corner of the room
        Document.Create.NewFamilyInstance(pt(316, 14), doorType, wallVert1, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
        
        // Kitchen: pushed to left corner
        Document.Create.NewFamilyInstance(pt(302, 14), doorType, wallHorizLeft, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
        
        // Bathroom: pushed to left corner of the bathroom
        Document.Create.NewFamilyInstance(pt(311.5, 14), doorType, wallHorizLeft, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 

        t.Commit();
        sb.AppendLine("5 new doors placed to the sides successfully.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Error moving doors: {ex.Message}");
    }
}

return sb.ToString();
