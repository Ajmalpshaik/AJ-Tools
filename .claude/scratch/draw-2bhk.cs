var sb = new System.Text.StringBuilder();

var level = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .FirstOrDefault(l => l.Name == "Level 1");

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

if (level == null || wallType == null)
{
    sb.AppendLine("Failed to find 'Level 1' or Basic WallType.");
    return sb.ToString();
}

using (var t = new Transaction(Document, "AJ Tools - Draw 2 BHK House"))
{
    t.Start();
    try
    {
        if (doorType != null && !doorType.IsActive) doorType.Activate();
        if (windowType != null && !windowType.IsActive) windowType.Activate();
        
        XYZ pt(double x, double y) => new XYZ(x, y, level.Elevation);
        
        Wall wallBottom = Wall.Create(Document, Line.CreateBound(pt(300, 0), pt(330, 0)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallTop = Wall.Create(Document, Line.CreateBound(pt(300, 24), pt(330, 24)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallLeft = Wall.Create(Document, Line.CreateBound(pt(300, 0), pt(300, 24)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallRight = Wall.Create(Document, Line.CreateBound(pt(330, 0), pt(330, 24)), wallType.Id, level.Id, 10.0, 0, false, false);
        
        Wall wallVert1 = Wall.Create(Document, Line.CreateBound(pt(316, 0), pt(316, 24)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallHorizLeft = Wall.Create(Document, Line.CreateBound(pt(300, 14), pt(316, 14)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallHorizRight = Wall.Create(Document, Line.CreateBound(pt(316, 12), pt(330, 12)), wallType.Id, level.Id, 10.0, 0, false, false);
        Wall wallVertBath = Wall.Create(Document, Line.CreateBound(pt(310, 14), pt(310, 24)), wallType.Id, level.Id, 10.0, 0, false, false);

        sb.AppendLine("Walls created successfully.");

        // Doors
        if (doorType != null) {
            Document.Create.NewFamilyInstance(pt(308, 0), doorType, wallBottom, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Front
            Document.Create.NewFamilyInstance(pt(316, 2), doorType, wallVert1, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Master Bed
            Document.Create.NewFamilyInstance(pt(316, 16), doorType, wallVert1, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Guest Bed
            Document.Create.NewFamilyInstance(pt(305, 14), doorType, wallHorizLeft, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Kitchen
            Document.Create.NewFamilyInstance(pt(313, 14), doorType, wallHorizLeft, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); // Bath
            sb.AppendLine("Doors created successfully.");
        }

        // Windows
        if (windowType != null) {
            void AddWindow(XYZ point, Wall host) {
                var w = Document.Create.NewFamilyInstance(point, windowType, host, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                w.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.Set(3.0);
            }
            
            AddWindow(pt(300, 7), wallLeft); // Living
            AddWindow(pt(304, 0), wallBottom); // Living
            AddWindow(pt(300, 19), wallLeft); // Kitchen
            AddWindow(pt(313, 24), wallTop); // Bath
            AddWindow(pt(330, 18), wallRight); // Guest
            AddWindow(pt(330, 6), wallRight); // Master
            AddWindow(pt(325, 0), wallBottom); // Master
            
            sb.AppendLine("7 Windows created successfully.");
        }

        // Floor
        if (floorType != null) {
            CurveArray profile = new CurveArray();
            profile.Append(Line.CreateBound(pt(300, 0), pt(330, 0)));
            profile.Append(Line.CreateBound(pt(330, 0), pt(330, 24)));
            profile.Append(Line.CreateBound(pt(330, 24), pt(300, 24)));
            profile.Append(Line.CreateBound(pt(300, 24), pt(300, 0)));
            Document.Create.NewFloor(profile, floorType, level, false);
            sb.AppendLine("Floor created successfully.");
        }

        t.Commit();
        sb.AppendLine("2 BHK House successfully drawn.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Error drawing house: {ex.Message}");
    }
}

return sb.ToString();
