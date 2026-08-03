var sb = new System.Text.StringBuilder();

var level = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .OrderBy(l => l.Elevation)
    .FirstOrDefault();

var wallType = new FilteredElementCollector(Document)
    .OfClass(typeof(WallType))
    .Cast<WallType>()
    .Where(wt => wt.Kind == WallKind.Basic)
    .FirstOrDefault();

var doorType = new FilteredElementCollector(Document)
    .OfClass(typeof(FamilySymbol))
    .OfCategory(BuiltInCategory.OST_Doors)
    .Cast<FamilySymbol>()
    .FirstOrDefault();

var windowType = new FilteredElementCollector(Document)
    .OfClass(typeof(FamilySymbol))
    .OfCategory(BuiltInCategory.OST_Windows)
    .Cast<FamilySymbol>()
    .FirstOrDefault();

var floorType = new FilteredElementCollector(Document)
    .OfClass(typeof(FloorType))
    .Cast<FloorType>()
    .FirstOrDefault();

using (var t = new Transaction(Document, "AJ Tools - Fix 1 BHK"))
{
    t.Start();
    try
    {
        // Delete old stuff at X >= 200
        var toDelete = new List<ElementId>();
        var allElems = new FilteredElementCollector(Document).WhereElementIsNotElementType().ToElements();
        foreach (var e in allElems) {
            if (e is Wall || e is Floor || e.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_Doors || e.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_Windows) {
                if (e.Location is LocationCurve lc && lc.Curve.GetEndPoint(0).X >= 199) toDelete.Add(e.Id);
                else if (e.Location is LocationPoint lp && lp.Point.X >= 199) toDelete.Add(e.Id);
                else if (e is Floor f) {
                    var bbox = f.get_BoundingBox(null);
                    if (bbox != null && bbox.Min.X > 190) toDelete.Add(e.Id);
                }
            }
        }
        foreach(var id in toDelete) {
            try { Document.Delete(id); } catch {}
        }
        sb.AppendLine($"Deleted {toDelete.Count} old elements.");

        if (doorType != null && !doorType.IsActive) doorType.Activate();
        if (windowType != null && !windowType.IsActive) windowType.Activate();
        
        XYZ pt(double x, double y) => new XYZ(x, y, level.Elevation);
        
        Wall wallBottom = Wall.Create(Document, Line.CreateBound(pt(200, 0), pt(224, 0)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallTop = Wall.Create(Document, Line.CreateBound(pt(200, 20), pt(224, 20)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallLeft = Wall.Create(Document, Line.CreateBound(pt(200, 0), pt(200, 20)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallRight = Wall.Create(Document, Line.CreateBound(pt(224, 0), pt(224, 20)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallMidVert = Wall.Create(Document, Line.CreateBound(pt(214, 0), pt(214, 20)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallMidHoriz = Wall.Create(Document, Line.CreateBound(pt(200, 12), pt(224, 12)), wallType.Id, level.Id, 10.0, 0, false, false);

        if (doorType != null) {
            Document.Create.NewFamilyInstance(pt(207, 0), doorType, wallBottom, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
            Document.Create.NewFamilyInstance(pt(214, 2), doorType, wallMidVert, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
            Document.Create.NewFamilyInstance(pt(207, 12), doorType, wallMidHoriz, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
            Document.Create.NewFamilyInstance(pt(219, 12), doorType, wallMidHoriz, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
        }

        if (windowType != null) {
            var w1 = Document.Create.NewFamilyInstance(pt(200, 6), windowType, wallLeft, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
            w1.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.Set(3.0); 
            var w2 = Document.Create.NewFamilyInstance(pt(224, 6), windowType, wallRight, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
            w2.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.Set(3.0);
            var w3 = Document.Create.NewFamilyInstance(pt(207, 20), windowType, wallTop, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
            w3.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.Set(3.0);
            var w4 = Document.Create.NewFamilyInstance(pt(219, 20), windowType, wallTop, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); 
            w4.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.Set(3.0);
        }

        if (floorType != null) {
            CurveArray profile = new CurveArray();
            profile.Append(Line.CreateBound(pt(200, 0), pt(224, 0)));
            profile.Append(Line.CreateBound(pt(224, 0), pt(224, 20)));
            profile.Append(Line.CreateBound(pt(224, 20), pt(200, 20)));
            profile.Append(Line.CreateBound(pt(200, 20), pt(200, 0)));
            Document.Create.NewFloor(profile, floorType, level, false);
        }

        t.Commit();
        sb.AppendLine("Fixed 1 BHK House successfully drawn.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Error drawing house: {ex.Message}");
    }
}

return sb.ToString();
