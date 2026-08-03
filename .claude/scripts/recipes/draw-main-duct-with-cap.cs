// ============================================================
// SCRIPT: draw-main-duct-with-cap.cs
// PURPOSE: Draw a single main duct piece from the FCU's supply connector along the room's long axis,
//          sized to the FCU connector, connected at the FCU end, and cap the open far end with the
//          full sized+positioned+rotated recipe (a bare ConnectTo does NOT prove the cap is correct).
// SOURCE:  ../knowledge/live-model/hvac-ducts.md § Drawing a duct between two points, § Capping an open duct end
// STATUS:  living document — refine in place, don't fork a v2 file. This is the most failure-prone
//          recipe in the set (real damage happened here before) — read the full narrative in
//          ../knowledge/live-model/hvac-ducts.md before changing this script, not just the code.
// ============================================================
// Does NOT include trunk-slicing for progressive duct sizing — that's a separate, higher-risk,
// ask-first request (see ../knowledge/live-model/hvac-ducts.md § Slicing a main trunk). This script draws ONE piece.
//
// The whole draw+connect+cap sequence runs inside ONE TransactionGroup: any failure at any step rolls
// back everything (duct AND cap), so a partial run never leaves an uncapped/half-connected duct behind
// — that exact partial-state failure mode is what caused real damage in this recipe before (technique
// adapted from an external repository's TransactionUtils
// ExecuteInTransactionGroup pattern — only the technique was taken, not any code; their helper can't be
// imported here since AJ AI Bridge scripts don't support cross-file `using` references).

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId fcuId = ElementId.InvalidElementId;
ElementId roomId = ElementId.InvalidElementId;
double wallClearanceMm = 750; // gap kept from the far wall, same convention as terminal placement
string ductTypeNameContains = "Rectangular Duct";
string capFamilyTypeNamePrefix = "AJ_AUTO_CAP_"; // duplicated type naming convention
// ---- END INPUTS ----

if (fcuId == ElementId.InvalidElementId || roomId == ElementId.InvalidElementId)
{
    return "Set fcuId and roomId explicitly in INPUTS before running.";
}

var fcu = Document.GetElement(fcuId) as FamilyInstance;
var room = Document.GetElement(roomId) as Autodesk.Revit.DB.Architecture.Room;
var supplyConn = fcu.MEPModel?.ConnectorManager?.Connectors?.Cast<Connector>()
    .FirstOrDefault(c => c.Domain == Domain.DomainHvac && c.DuctSystemType == DuctSystemType.SupplyAir && string.IsNullOrEmpty(c.Description));

if (supplyConn == null) return "No real supply-air connector found on the FCU (excluding the Fresh Air decoy).";

var terminals = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctTerminal)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Where(fi =>
    {
        var loc = (fi.Location as LocationPoint)?.Point;
        if (loc == null) return false;
        var testPt = new XYZ(loc.X, loc.Y, (room.Location as LocationPoint)?.Point.Z ?? loc.Z);
        return room.IsPointInRoom(testPt);
    })
    .ToList();

if (terminals.Count == 0) return "No terminals in this room yet — nothing to route the trunk toward.";

var bbox = room.get_BoundingBox(null);
double xExtent = bbox.Max.X - bbox.Min.X, yExtent = bbox.Max.Y - bbox.Min.Y;
bool xIsLong = xExtent >= yExtent;
double clearanceFt = UnitUtils.ConvertToInternalUnits(wallClearanceMm, DisplayUnitType.DUT_MILLIMETERS);

XYZ fcuOrigin = supplyConn.Origin;
var farthestTerm = terminals.OrderByDescending(t2 => fcuOrigin.DistanceTo((t2.Location as LocationPoint).Point)).First();
int sign = xIsLong
    ? Math.Sign((farthestTerm.Location as LocationPoint).Point.X - fcuOrigin.X)
    : Math.Sign((farthestTerm.Location as LocationPoint).Point.Y - fcuOrigin.Y);
if (sign == 0) sign = 1;

double wallCoord = xIsLong ? (sign > 0 ? bbox.Max.X : bbox.Min.X) : (sign > 0 ? bbox.Max.Y : bbox.Min.Y);
double endLongCoord = wallCoord - sign * clearanceFt;

XYZ startPt = fcuOrigin;
XYZ endPt = xIsLong ? new XYZ(endLongCoord, fcuOrigin.Y, fcuOrigin.Z) : new XYZ(fcuOrigin.X, endLongCoord, fcuOrigin.Z);

var sb = new System.Text.StringBuilder();

var ductType = new FilteredElementCollector(Document)
    .OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctType))
    .Cast<Autodesk.Revit.DB.Mechanical.DuctType>()
    .FirstOrDefault(dt => dt.FamilyName.IndexOf(ductTypeNameContains, StringComparison.OrdinalIgnoreCase) >= 0
        || dt.Name.IndexOf(ductTypeNameContains, StringComparison.OrdinalIgnoreCase) >= 0);
var systemType = new FilteredElementCollector(Document)
    .OfClass(typeof(MechanicalSystemType))
    .Cast<MechanicalSystemType>()
    .FirstOrDefault(st => st.SystemClassification == MEPSystemClassification.SupplyAir);

using (var group = new TransactionGroup(Document, "AJ Tools - Main Duct + Cap"))
{
    group.Start();
    try
    {
        Duct mainDuct;
        using (var t = new Transaction(Document, "AJ Tools - Draw Main Duct"))
        {
            t.Start();
            mainDuct = Duct.Create(Document, systemType.Id, ductType.Id, fcu.LevelId, startPt, endPt);
            mainDuct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.Set(supplyConn.Width);
            mainDuct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.Set(supplyConn.Height);
            t.Commit();
        }

        var ductConnectors = mainDuct.ConnectorManager.Connectors.Cast<Connector>().ToList();
        var fcuEndConn = ductConnectors.OrderBy(c => c.Origin.DistanceTo(startPt)).First();
        var openEndConn = ductConnectors.OrderBy(c => c.Origin.DistanceTo(endPt)).First();

        using (var t = new Transaction(Document, "AJ Tools - Connect FCU End"))
        {
            t.Start();
            supplyConn.ConnectTo(fcuEndConn);
            t.Commit();
        }
        sb.AppendLine($"Drew main duct, FCU end connected. Size {UnitUtils.ConvertFromInternalUnits(supplyConn.Width, DisplayUnitType.DUT_MILLIMETERS):F0}x{UnitUtils.ConvertFromInternalUnits(supplyConn.Height, DisplayUnitType.DUT_MILLIMETERS):F0}mm.");

        // --- Cap the open far end: get type from Routing Preferences, duplicate a sized type, place,
        // --- size, move, rotate, re-move, connect. See ../knowledge/live-model/hvac-ducts.md for why every step is needed.
        var capRule = ductType.RoutingPreferenceManager.GetRule(RoutingPreferenceRuleGroupType.Caps, 0);
        var capSymbolId = capRule?.MEPPartId;
        var capBaseSymbol = capSymbolId != null ? Document.GetElement(capSymbolId) as FamilySymbol : null;

        if (capBaseSymbol == null)
        {
            // No cap type available — this is a genuine "can't finish safely" case, not a bug to
            // swallow: roll back the whole group so we never leave an uncapped duct behind silently.
            throw new Exception("No cap type found in this duct type's Routing Preferences.");
        }

        double widthMm = UnitUtils.ConvertFromInternalUnits(supplyConn.Width, DisplayUnitType.DUT_MILLIMETERS);
        double heightMm = UnitUtils.ConvertFromInternalUnits(supplyConn.Height, DisplayUnitType.DUT_MILLIMETERS);
        string sizedTypeName = $"{capFamilyTypeNamePrefix}{widthMm:F0}x{heightMm:F0}mm";

        var existingSizedType = new FilteredElementCollector(Document)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault(fs => fs.Name == sizedTypeName && fs.Family.Id == capBaseSymbol.Family.Id);

        FamilySymbol capSymbol;
        using (var t = new Transaction(Document, "AJ Tools - Prepare Cap Type"))
        {
            t.Start();
            if (existingSizedType != null)
            {
                capSymbol = existingSizedType;
            }
            else
            {
                capSymbol = capBaseSymbol.Duplicate(sizedTypeName) as FamilySymbol;
                foreach (Parameter p in capSymbol.Parameters)
                {
                    if (p.IsReadOnly || p.StorageType != StorageType.Double) continue;
                    var n = p.Definition.Name.ToUpperInvariant();
                    if (n.Contains("WIDTH") || n == "W") p.Set(supplyConn.Width);
                    if (n.Contains("HEIGHT") || n == "H") p.Set(supplyConn.Height);
                }
            }
            if (!capSymbol.IsActive) capSymbol.Activate();
            t.Commit();
        }

        using (var t = new Transaction(Document, "AJ Tools - Place Cap"))
        {
            t.Start();
            var cap = Document.Create.NewFamilyInstance(openEndConn.Origin, capSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            foreach (Parameter p in cap.Parameters)
            {
                if (p.IsReadOnly || p.StorageType != StorageType.Double) continue;
                var n = p.Definition.Name.ToUpperInvariant();
                if (n.Contains("WIDTH") || n == "W") p.Set(supplyConn.Width);
                if (n.Contains("HEIGHT") || n == "H") p.Set(supplyConn.Height);
            }
            Document.Regenerate();

            var capConn = cap.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                .OrderBy(c => c.Origin.DistanceTo(openEndConn.Origin)).First();
            capConn.Width = openEndConn.Width;
            capConn.Height = openEndConn.Height;
            Document.Regenerate();

            capConn = cap.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                .OrderBy(c => c.Origin.DistanceTo(openEndConn.Origin)).First();
            ElementTransformUtils.MoveElement(Document, cap.Id, openEndConn.Origin - capConn.Origin);
            Document.Regenerate();

            capConn = cap.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                .OrderBy(c => c.Origin.DistanceTo(openEndConn.Origin)).First();
            var targetDir = -openEndConn.CoordinateSystem.BasisZ;
            double angle = capConn.CoordinateSystem.BasisZ.AngleTo(targetDir);
            if (angle > 0.0001)
            {
                var axis = capConn.CoordinateSystem.BasisZ.CrossProduct(targetDir);
                if (axis.IsZeroLength()) axis = capConn.CoordinateSystem.BasisZ.CrossProduct(XYZ.BasisX);
                if (axis.IsZeroLength()) axis = capConn.CoordinateSystem.BasisZ.CrossProduct(XYZ.BasisY);
                ElementTransformUtils.RotateElement(Document, cap.Id, Line.CreateBound(openEndConn.Origin, openEndConn.Origin + axis), angle);
                Document.Regenerate();
            }

            capConn = cap.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                .OrderBy(c => c.Origin.DistanceTo(openEndConn.Origin)).First();
            ElementTransformUtils.MoveElement(Document, cap.Id, openEndConn.Origin - capConn.Origin);
            Document.Regenerate();

            capConn = cap.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                .OrderBy(c => c.Origin.DistanceTo(openEndConn.Origin)).First();
            capConn.ConnectTo(openEndConn);

            t.Commit();
        }

        sb.AppendLine($"Capped the open end with type '{sizedTypeName}' — sized, positioned, and rotated (not just ConnectTo).");
        group.Assimilate(); // commits the whole group as one Undo-able step, only reached on full success
    }
    catch (Exception ex)
    {
        group.RollBack(); // undoes the duct AND any partial cap work — never leaves a half-built state
        return $"FAILED — rolled back the entire main duct + cap sequence, nothing left behind. Reason: {ex.Message}";
    }
}

return sb.ToString();
