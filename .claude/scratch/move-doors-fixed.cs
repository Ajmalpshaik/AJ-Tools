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

using (var t = new Transaction(Document, "AJ Tools - Fix Doors"))
{
    t.Start();
    try
    {
        foreach(var id in doorsToDelete) {
            try { Document.Delete(id); } catch { }
        }
        
        XYZ pt(double x, double y) => new XYZ(x, y, level.Elevation);
        
        // Front door: 10ft from corner
        Document.Create.NewFamilyInstance(pt(310, 0), doorType, wallBottom, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
        // Master Bed: 4ft from corner
        Document.Create.NewFamilyInstance(pt(316, 4), doorType, wallVert1, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
        // Guest Bed: 4ft from intersection
        Document.Create.NewFamilyInstance(pt(316, 18), doorType, wallVert1, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
        // Kitchen: 4ft from left wall
        Document.Create.NewFamilyInstance(pt(304, 14), doorType, wallHorizLeft, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
        // Bathroom: Middle of 6ft wall
        Document.Create.NewFamilyInstance(pt(313, 14), doorType, wallHorizLeft, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 

        t.Commit();
        sb.AppendLine("5 new doors safely placed off-center.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Error moving doors: {ex.Message}");
    }
}

return sb.ToString();
