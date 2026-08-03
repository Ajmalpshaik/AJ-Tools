// ============================================================
// SCRIPT: connect-terminal-branch.cs
// PURPOSE: Connect one air terminal to the main duct — vertical riser up to the main duct's height,
//          a real elbow fitting at the turn, then a horizontal run tapped into the main duct via a
//          takeoff tee. Skips the horizontal segment entirely if the terminal already lines up under
//          the main duct's line (near-zero offset would throw a minimum-length error).
// SOURCE:  ../knowledge/live-model/hvac-ducts.md § Branch duct from a terminal to a main duct
// STATUS:  living document — refine in place, don't fork a v2 file.
// ============================================================
// Because supply/return terminals are checkerboard-alternated, "nearest terminal" is frequently the
// WRONG system type — this script is meant to be called once per terminal already filtered by system.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId terminalId = ElementId.InvalidElementId;
ElementId mainDuctId = ElementId.InvalidElementId; // the true trunk segment, not a previously-placed branch
// ---- END INPUTS ----

if (terminalId == ElementId.InvalidElementId || mainDuctId == ElementId.InvalidElementId)
{
    return "Set terminalId and mainDuctId explicitly in INPUTS before running.";
}

var terminal = Document.GetElement(terminalId) as FamilyInstance;
var mainDuct = Document.GetElement(mainDuctId) as Duct;
if (terminal == null || mainDuct == null)
{
    return "terminalId or mainDuctId does not point to the expected element type.";
}

var terminalConnectorSet = terminal.MEPModel?.ConnectorManager?.Connectors;
var termConn = terminalConnectorSet == null
    ? null
    : terminalConnectorSet.Cast<Connector>().FirstOrDefault(c => c.Domain == Domain.DomainHvac);
if (termConn == null) return "No HVAC connector found on the terminal.";

var mainCurve = (mainDuct.Location as LocationCurve)?.Curve;
if (mainCurve == null) return "Main duct does not have a valid location curve.";

double mainZ = mainCurve.GetEndPoint(0).Z;

var sb = new System.Text.StringBuilder();

ElementId ductTypeId = mainDuct.GetTypeId();
ElementId branchLevelId = mainDuct.LevelId != ElementId.InvalidElementId ? mainDuct.LevelId : terminal.LevelId;
if (branchLevelId == ElementId.InvalidElementId) return "Could not resolve a level for the branch duct.";

ElementId systemTypeId = ElementId.InvalidElementId;
var systemTypeParam = mainDuct.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
if (systemTypeParam != null) systemTypeId = systemTypeParam.AsElementId();
if (systemTypeId == ElementId.InvalidElementId && mainDuct.MEPSystem != null)
{
    systemTypeId = mainDuct.MEPSystem.GetTypeId();
}
if (systemTypeId == ElementId.InvalidElementId)
{
    var fallbackSystemType = new FilteredElementCollector(Document)
        .OfClass(typeof(MechanicalSystemType))
        .Cast<MechanicalSystemType>()
        .FirstOrDefault(st => st.SystemClassification == MEPSystemClassification.SupplyAir);
    if (fallbackSystemType == null) return "Could not resolve a duct system type for the branch.";
    systemTypeId = fallbackSystemType.Id;
    sb.AppendLine("WARNING: main duct system type was not readable - used first Supply Air system type as fallback.");
}

XYZ riserTop = new XYZ(termConn.Origin.X, termConn.Origin.Y, mainZ);
var projectedToMain = mainCurve.Project(riserTop);
if (projectedToMain == null) return "Terminal does not project onto the main duct curve.";

var tapPointOnMain = projectedToMain.XYZPoint;
double distToTermXY = new XYZ(termConn.Origin.X, termConn.Origin.Y, 0)
    .DistanceTo(new XYZ(tapPointOnMain.X, tapPointOnMain.Y, 0));

using (var t = new Transaction(Document, "AJ Tools - Connect Terminal Branch"))
{
    t.Start();
    try
    {

    // Vertical riser.
    var riser = Duct.Create(Document, systemTypeId, ductTypeId, branchLevelId, termConn.Origin, riserTop);
    riser.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.Set(termConn.Width);
    riser.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.Set(termConn.Height);
    Document.Regenerate();

    var riserConns = riser.ConnectorManager.Connectors.Cast<Connector>().ToList();
    var riserBottom = riserConns.OrderBy(c => c.Origin.DistanceTo(termConn.Origin)).First();
    var riserTopConn = riserConns.OrderBy(c => c.Origin.DistanceTo(riserTop)).First();
    termConn.ConnectTo(riserBottom);

    double minLengthFt = UnitUtils.ConvertToInternalUnits(3, DisplayUnitType.DUT_MILLIMETERS); // ~1/10 inch guard

    if (distToTermXY < minLengthFt)
    {
        // Near-zero offset — tap the riser's own top connector straight into the main duct, no elbow.
        Document.Create.NewTakeoffFitting(riserTopConn, mainDuct);
        sb.AppendLine("Terminal lines up under the main duct — riser tapped directly, no elbow needed.");
    }
    else
    {
        var tapPoint = tapPointOnMain;
        var horiz = Duct.Create(Document, systemTypeId, ductTypeId, branchLevelId, riserTop, tapPoint);
        horiz.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.Set(termConn.Width);
        horiz.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.Set(termConn.Height);
        Document.Regenerate();

        var horizConns = horiz.ConnectorManager.Connectors.Cast<Connector>().ToList();
        var horizNearRiser = horizConns.OrderBy(c => c.Origin.DistanceTo(riserTop)).First();
        var horizFar = horizConns.OrderBy(c => c.Origin.DistanceTo(tapPoint)).First();

        // Real elbow fitting at the turn — NOT a bare ConnectTo (that makes a logical link, no geometry).
        Document.Create.NewElbowFitting(riserTopConn, horizNearRiser);
        Document.Create.NewTakeoffFitting(horizFar, mainDuct);
        sb.AppendLine("Riser + elbow + horizontal run, tapped into main duct via takeoff.");
    }

    t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        return $"FAILED - rolled back branch connection, nothing changed. Reason: {ex.Message}";
    }
}

return sb.ToString();
