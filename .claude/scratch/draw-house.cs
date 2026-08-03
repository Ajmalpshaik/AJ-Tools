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

if (level == null || wallType == null)
{
    sb.AppendLine("Failed to find a Level or Basic WallType.");
    return sb.ToString();
}

double ToFt(double mm) => UnitUtils.ConvertToInternalUnits(mm, DisplayUnitType.DUT_MILLIMETERS);

// Define layout lines in mm
var linesMm = new List<(double x1, double y1, double x2, double y2)>
{
    // Perimeter
    (0, 0, 10000, 0),
    (10000, 0, 10000, 8000),
    (10000, 8000, 0, 8000),
    (0, 8000, 0, 0),
    // Vertical interior partition at X = 4000
    (4000, 0, 4000, 8000),
    // Horizontal interior partition left (Bedroom split)
    (0, 4000, 4000, 4000),
    // Horizontal interior partition right (Kitchen/Hall split)
    (4000, 4000, 10000, 4000)
};

int count = 0;
using (var t = new Transaction(Document, "AJ Tools - Draw House Plan"))
{
    t.Start();
    try
    {
        foreach (var l in linesMm)
        {
            var p1 = new XYZ(ToFt(l.x1), ToFt(l.y1), 0);
            var p2 = new XYZ(ToFt(l.x2), ToFt(l.y2), 0);
            var curve = Line.CreateBound(p1, p2);
            // Default wall height 3000mm (3 meters)
            Wall.Create(Document, curve, wallType.Id, level.Id, ToFt(3000), 0, false, false);
            count++;
        }
        t.Commit();
        sb.AppendLine($"Successfully drew {count} walls for the house layout on {level.Name} using {wallType.Name}.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Error drawing house: {ex.Message}");
    }
}

return sb.ToString();
