// ============================================================
// SCRIPT: place-terminals-checkerboard.cs
// PURPOSE: Place a room's brand-new supply/return air terminals in a near-square checkerboard grid
//          with matched supply/return counts, and set each instance's own Flow parameter.
// SOURCE:  ../knowledge/live-model/hvac-terminals.md § HVAC air terminal layout (checkerboard grid, near-square row count,
//          grid orientation auto-detect, duplicate-"Flow"-parameter gotcha)
// STATUS:  living document — refine in place, don't fork a v2 file.
// ============================================================
// Assumes the room's Space already has correct Specified Supply/Return Airflow (run
// set-space-airflow.cs first if not). Every number below is a per-request input.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId roomId = ElementId.InvalidElementId;       // set explicitly
FamilySymbol supplySymbol = null;                    // resolve via FilteredElementCollector by family name
FamilySymbol returnSymbol = null;
double maxLsPerTerminal = 200;
int minCount = 2;
double wallClearanceMm = 500;
double placementHeightMm = 2400; // fallback if the room's real Ceiling element isn't found
// ---- END INPUTS ----

if (roomId == ElementId.InvalidElementId || supplySymbol == null || returnSymbol == null)
{
    return "Set roomId, supplySymbol, and returnSymbol explicitly in INPUTS before running.";
}

var room = Document.GetElement(roomId) as Autodesk.Revit.DB.Architecture.Room;
if (room == null) return "roomId does not point to a valid Room.";

var roomCenter = (room.Location as LocationPoint)?.Point ?? XYZ.Zero;
var space = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_MEPSpaces)
    .WhereElementIsNotElementType()
    .Cast<Autodesk.Revit.DB.Mechanical.Space>()
    .FirstOrDefault(s =>
    {
        var loc = (s.Location as LocationPoint)?.Point;
        if (loc == null) return false;
        return room.IsPointInRoom(new XYZ(loc.X, loc.Y, roomCenter.Z));
    });

if (space == null) return "No Space found for this room — run set-space-airflow.cs first.";

double supplyLs = UnitUtils.ConvertFromInternalUnits(space.get_Parameter(BuiltInParameter.ROOM_DESIGN_SUPPLY_AIRFLOW_PARAM).AsDouble(), DisplayUnitType.DUT_LITERS_PER_SECOND);
double returnLs = UnitUtils.ConvertFromInternalUnits(space.get_Parameter(BuiltInParameter.ROOM_DESIGN_RETURN_AIRFLOW_PARAM).AsDouble(), DisplayUnitType.DUT_LITERS_PER_SECOND);

int count = Math.Max(minCount, (int)Math.Ceiling(supplyLs / maxLsPerTerminal)); // supply's count used for BOTH

// Room bbox, shrunk by clearance.
var bbox = room.get_BoundingBox(null);
double clearanceFt = UnitUtils.ConvertToInternalUnits(wallClearanceMm, DisplayUnitType.DUT_MILLIMETERS);
double xMin = bbox.Min.X + clearanceFt, xMax = bbox.Max.X - clearanceFt;
double yMin = bbox.Min.Y + clearanceFt, yMax = bbox.Max.Y - clearanceFt;
double xExtent = xMax - xMin, yExtent = yMax - yMin;

// Auto-detect long axis — 2-row axis along the shorter extent, N-column axis along the longer.
bool xIsLong = xExtent >= yExtent;
double shortExtent = xIsLong ? yExtent : xExtent;
double longExtent = xIsLong ? xExtent : yExtent;

int totalCells = count * 2;
int rows = (int)Math.Round(Math.Sqrt(totalCells * (shortExtent / longExtent)));
rows = Math.Max(1, Math.Min(rows, totalCells));
int baseCols = totalCells / rows;
int remainder = totalCells % rows;

// Ceiling height.
Func<Element, bool> elementCenterIsInRoom = e =>
{
    var eb = e.get_BoundingBox(null);
    if (eb == null) return false;
    var center = (eb.Min + eb.Max) * 0.5;
    return room.IsPointInRoom(new XYZ(center.X, center.Y, roomCenter.Z));
};

var ceiling = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_Ceilings)
    .WhereElementIsNotElementType()
    .Cast<Ceiling>()
    .FirstOrDefault(c => elementCenterIsInRoom(c));
double heightMm = ceiling?.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM)?.AsDouble() != null
    ? UnitUtils.ConvertFromInternalUnits(ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).AsDouble(), DisplayUnitType.DUT_MILLIMETERS)
    : placementHeightMm;
double z = bbox.Min.Z + UnitUtils.ConvertToInternalUnits(heightMm, DisplayUnitType.DUT_MILLIMETERS);

var sb = new System.Text.StringBuilder();
sb.AppendLine($"Room {room.Name}: {count} supply + {count} return, {rows} row(s), near-square grid.");

var placedInstances = new List<FamilyInstance>();

using (var t = new Transaction(Document, "AJ Tools - Place Air Terminals"))
{
    t.Start();
    if (!supplySymbol.IsActive) supplySymbol.Activate();
    if (!returnSymbol.IsActive) returnSymbol.Activate();

    int placed = 0, cellGlobalIndex = 0;
    for (int row = 0; row < rows; row++)
    {
        int colsThisRow = baseCols + (row < remainder ? 1 : 0);
        for (int col = 0; col < colsThisRow; col++)
        {
            double tFrac = colsThisRow <= 1 ? 0.5 : (double)col / (colsThisRow - 1);
            double rFrac = rows <= 1 ? 0.5 : (double)row / (rows - 1);

            double longPos = (xIsLong ? xMin : yMin) + tFrac * longExtent;
            double shortPos = (xIsLong ? yMin : xMin) + rFrac * shortExtent;
            XYZ pt = xIsLong ? new XYZ(longPos, shortPos, z) : new XYZ(shortPos, longPos, z);

            bool isSupply = (row + col) % 2 == 0; // true checkerboard — NOT continuous-index
            var symbol = isSupply ? supplySymbol : returnSymbol;

            var level = Document.GetElement(room.LevelId) as Level;
            var fi = Document.Create.NewFamilyInstance(pt, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            placedInstances.Add(fi);
            placed++;
            cellGlobalIndex++;
        }
    }
    t.Commit();

    // Set each newly placed terminal's own Flow parameter (individual share), per system type.
    var supplyTerms = placedInstances.Where(fi => fi.Symbol.Id == supplySymbol.Id).ToList();
    var returnTerms = placedInstances.Where(fi => fi.Symbol.Id == returnSymbol.Id).ToList();

    using (var t2 = new Transaction(Document, "AJ Tools - Set Terminal Flow"))
    {
        t2.Start();
        double supplyEach = UnitUtils.ConvertToInternalUnits(supplyLs / Math.Max(1, supplyTerms.Count), DisplayUnitType.DUT_LITERS_PER_SECOND);
        double returnEach = UnitUtils.ConvertToInternalUnits(returnLs / Math.Max(1, returnTerms.Count), DisplayUnitType.DUT_LITERS_PER_SECOND);
        foreach (var fi in supplyTerms) fi.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM)?.Set(supplyEach);
        foreach (var fi in returnTerms) fi.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM)?.Set(returnEach);
        t2.Commit();
    }

    sb.AppendLine($"Placed {placed} terminal(s). Verify actual (row,col) type pattern after placing — don't trust a distance-based check.");
}

return sb.ToString();
