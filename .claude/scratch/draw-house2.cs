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

// Define layout lines in feet
var linesFt = new List<(double x1, double y1, double x2, double y2)>
{
    // Exterior Perimeter
    (100.0, 32.08, 117.5, 32.08), // Top
    (100.0, 0.0, 100.0, 32.08), // Left
    (117.5, 0.0, 117.5, 32.08), // Right
    (108.5, 0.0, 117.5, 0.0), // Bottom Stairs

    // Horizontal Partitions
    (100.0, 22.5, 117.5, 22.5), // Kitchen/Bed vs Living
    (100.0, 8.5, 117.5, 8.5), // Living vs Porch/Stairs

    // Vertical Partitions
    (106.5, 22.5, 106.5, 32.08), // Kitchen vs Bed
    (108.5, 0.0, 108.5, 8.5) // Porch vs Stairs
};

int count = 0;
using (var t = new Transaction(Document, "AJ Tools - Draw Image House Plan"))
{
    t.Start();
    try
    {
        foreach (var l in linesFt)
        {
            var p1 = new XYZ(l.x1, l.y1, 0);
            var p2 = new XYZ(l.x2, l.y2, 0);
            var curve = Line.CreateBound(p1, p2);
            // Default wall height 10 feet
            Wall.Create(Document, curve, wallType.Id, level.Id, 10.0, 0, false, false);
            count++;
        }
        t.Commit();
        sb.AppendLine($"Successfully drew {count} walls for the image house layout on {level.Name} using {wallType.Name}.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"Error drawing house: {ex.Message}");
    }
}

return sb.ToString();
