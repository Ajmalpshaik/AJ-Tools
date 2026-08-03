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
    .FirstOrDefault(t => t.IsActive || t.Name != null);

var windowType = new FilteredElementCollector(Document)
    .OfClass(typeof(FamilySymbol))
    .OfCategory(BuiltInCategory.OST_Windows)
    .Cast<FamilySymbol>()
    .FirstOrDefault(t => t.IsActive || t.Name != null);

var floorType = new FilteredElementCollector(Document)
    .OfClass(typeof(FloorType))
    .Cast<FloorType>()
    .FirstOrDefault();

if (level == null || wallType == null)
{
    sb.AppendLine("Failed to find a Level or Basic WallType.");
    return sb.ToString();
}

using (var t = new Transaction(Document, "AJ Tools - Draw Complete 1 BHK"))
{
    t.Start();
    try
    {
        if (doorType != null && !doorType.IsActive) doorType.Activate();
        if (windowType != null && !windowType.IsActive) windowType.Activate();
        
        XYZ pt(double x, double y) => new XYZ(x, y, 0);
        
        Wall wallBottom = Wall.Create(Document, Line.CreateBound(pt(200, 0), pt(224, 0)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallTop = Wall.Create(Document, Line.CreateBound(pt(200, 20), pt(224, 20)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallLeft = Wall.Create(Document, Line.CreateBound(pt(200, 0), pt(200, 20)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallRight = Wall.Create(Document, Line.CreateBound(pt(224, 0), pt(224, 20)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallMidVert = Wall.Create(Document, Line.CreateBound(pt(214, 0), pt(214, 20)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallMidHoriz = Wall.Create(Document, Line.CreateBound(pt(200, 12), pt(224, 12)), wallType.Id, level.Id, 10.0, 0, false, false);

        sb.AppendLine("Walls created successfully.");

        // Doors
        if (doorType != null) {
            Document.Create.NewFamilyInstance(pt(207, 0), doorType, wallBottom, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Front door
            Document.Create.NewFamilyInstance(pt(214, 2), doorType, wallMidVert, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Bedroom door
            Document.Create.NewFamilyInstance(pt(207, 12), doorType, wallMidHoriz, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Kitchen door
            Document.Create.NewFamilyInstance(pt(219, 12), doorType, wallMidHoriz, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Bathroom door
            sb.AppendLine("Doors created successfully.");
        }

        // Windows
        if (windowType != null) {
            var w1 = Document.Create.NewFamilyInstance(pt(200, 6), windowType, wallLeft, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Living
            w1.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.Set(3.0); // 3ft sill height
            var w2 = Document.Create.NewFamilyInstance(pt(224, 6), windowType, wallRight, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Bedroom
            w2.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.Set(3.0);
            var w3 = Document.Create.NewFamilyInstance(pt(207, 20), windowType, wallTop, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Kitchen
            w3.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.Set(3.0);
            var w4 = Document.Create.NewFamilyInstance(pt(219, 20), windowType, wallTop, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Bathroom
            w4.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.Set(3.0);
            sb.AppendLine("Windows created successfully.");
        }

        // Floor
        if (floorType != null) {
            CurveArray profile = new CurveArray();
            profile.Append(Line.CreateBound(pt(200, 0), pt(224, 0)));
            profile.Append(Line.CreateBound(pt(224, 0), pt(224, 20)));
            profile.Append(Line.CreateBound(pt(224, 20), pt(200, 20)));
            profile.Append(Line.CreateBound(pt(200, 20), pt(200, 0)));
            Document.Create.NewFloor(profile, floorType, level, false);
            sb.AppendLine("Floor created successfully.");
        }

        t.Commit();
        sb.AppendLine("1 BHK House successfully drawn.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Error drawing house: {ex.Message}");
    }
}

return sb.ToString();
