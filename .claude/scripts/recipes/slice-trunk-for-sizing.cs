// ============================================================
// SCRIPT: slice-trunk-for-sizing.cs
// PURPOSE: Slice a main HVAC trunk duct into separate segments at each terminal-branch takeoff point,
//          offset downstream past the takeoff's own body + a clearance margin, so each resulting segment
//          can later be individually sized down after its branch removes some airflow. Skips the cut after
//          the LAST takeoff before the end cap (that segment runs through in one piece to the cap).
// SOURCE:  ../knowledge/live-model/hvac-ducts.md § Slicing a main trunk into segments for duct sizing
// STATUS:  living document - refine in place, don't fork a v2 file.
// ============================================================
// HIGH RISK - a past session's first attempt (slice at the takeoff's own center, then relocate the joint
// by editing LocationCurve.Curve) silently deleted a takeoff fitting and orphaned its whole branch, with
// the terminal's own IsConnected still reading true. This script uses the corrected technique instead:
// compute the offset break point FIRST, then BreakCurve directly at that point on the still-whole trunk -
// never slice-then-relocate. Even so: test on one room/trunk first, then run
// verify-duct-connectivity.cs (full BFS trace, not just IsConnected) before rolling out further, and check
// with Ajmal after the first one. Live-verified 2026-07-17: 3 cuts on a 4-row/8-terminal trunk, all 8
// terminals still traced to the FCU afterward.

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
ElementId trunkPieceId = ElementId.InvalidElementId; // any one Duct element that is part of the trunk
double marginMm = 500;              // clearance downstream of each takeoff's own half-width, per-request
double groupToleranceMm = 50;       // takeoffs within this distance along the trunk are one cut point
                                     // (checkerboard rooms put two takeoffs at ~the same position from
                                     // opposite sides - group them so you don't try to cut the same point twice)
bool skipLastTakeoff = true;        // per convention: never cut after the takeoff closest to the end cap
// ---- END INPUTS ----

if (trunkPieceId == ElementId.InvalidElementId)
{
    return "Set trunkPieceId explicitly in INPUTS before running - any one Duct element that's part of the trunk.";
}

var sb = new System.Text.StringBuilder();

var startPiece = Document.GetElement(trunkPieceId) as Autodesk.Revit.DB.Mechanical.Duct;
if (startPiece == null) return "trunkPieceId does not point to a Duct element.";

var startCurve = (startPiece.Location as LocationCurve)?.Curve as Line;
if (startCurve == null) return "Trunk piece does not have a straight LocationCurve.";

XYZ trunkDir = (startCurve.GetEndPoint(1) - startCurve.GetEndPoint(0)).Normalize();
XYZ refPoint = startCurve.GetEndPoint(0);
double widthFt = startPiece.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.AsDouble()
    ?? startPiece.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.AsDouble() ?? 0;
double halfWidthFt = widthFt / 2.0;
double marginFt = UnitUtils.ConvertToInternalUnits(marginMm, DisplayUnitType.DUT_MILLIMETERS);
double groupToleranceFt = UnitUtils.ConvertToInternalUnits(groupToleranceMm, DisplayUnitType.DUT_MILLIMETERS);
double offsetFt = halfWidthFt + marginFt;

// Find every collinear trunk piece (same line as the starting piece) so multi-piece trunks are handled too.
Func<Autodesk.Revit.DB.Mechanical.Duct, bool> onTrunkLine = d =>
{
    var lc = (d.Location as LocationCurve)?.Curve as Line;
    if (lc == null) return false;
    return startCurve.Distance(lc.GetEndPoint(0)) < 0.05 && startCurve.Distance(lc.GetEndPoint(1)) < 0.05
        && (lc.Direction.IsAlmostEqualTo(trunkDir) || lc.Direction.IsAlmostEqualTo(-trunkDir));
};
Func<List<Autodesk.Revit.DB.Mechanical.Duct>> findTrunkPieces = () =>
    new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctCurves)
        .WhereElementIsNotElementType().Cast<Autodesk.Revit.DB.Mechanical.Duct>()
        .Where(onTrunkLine).ToList();

// Collect every takeoff (Curve-type) connector across all current trunk pieces, projected to a distance
// along trunkDir from refPoint.
var takeoffDistances = new List<double>();
foreach (var piece in findTrunkPieces())
{
    foreach (Connector c in piece.ConnectorManager.Connectors)
    {
        if (c.ConnectorType != ConnectorType.Curve) continue;
        double dist = (c.Origin - refPoint).DotProduct(trunkDir);
        takeoffDistances.Add(dist);
    }
}
if (takeoffDistances.Count == 0) return "No takeoff (Curve-type) connectors found on the trunk - nothing to slice at.";

// Group nearby takeoffs (checkerboard: two terminals per row tap at ~the same position from opposite sides).
takeoffDistances.Sort();
var groups = new List<double>(); // one representative distance per group
double groupStart = takeoffDistances[0];
double groupSum = takeoffDistances[0];
int groupCount = 1;
for (int i = 1; i < takeoffDistances.Count; i++)
{
    if (takeoffDistances[i] - takeoffDistances[i - 1] <= groupToleranceFt)
    {
        groupSum += takeoffDistances[i];
        groupCount++;
    }
    else
    {
        groups.Add(groupSum / groupCount);
        groupStart = takeoffDistances[i];
        groupSum = takeoffDistances[i];
        groupCount = 1;
    }
}
groups.Add(groupSum / groupCount);
sb.AppendLine($"Found {takeoffDistances.Count} takeoff connector(s) grouped into {groups.Count} cut position(s) (tolerance {groupToleranceMm}mm).");

if (skipLastTakeoff && groups.Count > 0)
{
    sb.AppendLine($"Skipping the cut after the last group (closest to the end cap), per convention.");
    groups.RemoveAt(groups.Count - 1);
}

// Re-locate the correct current trunk piece for each cut geometrically (BreakCurve reassigns which
// element Id keeps which segment, so don't trust an Id across cuts).
Func<double, Autodesk.Revit.DB.Mechanical.Duct> findPieceContainingDistance = (d) =>
{
    foreach (var piece in findTrunkPieces())
    {
        var lc = (piece.Location as LocationCurve).Curve as Line;
        double d0 = (lc.GetEndPoint(0) - refPoint).DotProduct(trunkDir);
        double d1 = (lc.GetEndPoint(1) - refPoint).DotProduct(trunkDir);
        double dMin = Math.Min(d0, d1), dMax = Math.Max(d0, d1);
        if (d > dMin + 0.05 && d < dMax - 0.05) return piece;
    }
    return null;
};

int cutCount = 0, failCount = 0;
foreach (var groupDist in groups)
{
    double cutDist = groupDist + offsetFt;
    var piece = findPieceContainingDistance(cutDist);
    if (piece == null)
    {
        sb.AppendLine($"Cut at distance {cutDist*304.8:F0}mm: FAILED - no current trunk piece contains this point (offset may run past the next group or off the trunk's end).");
        failCount++;
        continue;
    }

    XYZ breakPt = refPoint + trunkDir * cutDist;
    using (var t = new Transaction(Document, "AJ Tools - Slice Trunk for Sizing"))
    {
        t.Start();
        try
        {
            var newId = Autodesk.Revit.DB.Mechanical.MechanicalUtils.BreakCurve(Document, piece.Id, breakPt);
            var origFresh = Document.GetElement(piece.Id) as Autodesk.Revit.DB.Mechanical.Duct;
            var newFresh = Document.GetElement(newId) as Autodesk.Revit.DB.Mechanical.Duct;
            var origConn = origFresh.ConnectorManager.Connectors.Cast<Connector>()
                .Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
            var newConn = newFresh.ConnectorManager.Connectors.Cast<Connector>()
                .Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
            origConn.ConnectTo(newConn);
            t.Commit();
            sb.AppendLine($"Cut at {cutDist*304.8:F0}mm along trunk: split piece {piece.Id} -> new piece {newId}, joint reconnected (IsConnected={origConn.IsConnected && newConn.IsConnected}).");
            cutCount++;
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"Cut at {cutDist*304.8:F0}mm: FAILED - rolled back, nothing changed. Reason: {ex.Message}");
            failCount++;
        }
    }
}

sb.AppendLine($"\n{cutCount} cut(s) made, {failCount} failed.");
sb.AppendLine("IMPORTANT: now run verify-duct-connectivity.cs (full BFS trace per terminal) before trusting this - " +
    "never rely on IsConnected alone after a trunk slice.");
return sb.ToString();
