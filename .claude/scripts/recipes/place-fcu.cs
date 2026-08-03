// ============================================================
// SCRIPT: place-fcu.cs
// PURPOSE: Place an FCU (Mechanical Equipment) in a room at the given ceiling-void height, optionally
//          shift it toward the room's door (perpendicular-to-wall axis only), and rotate its real
//          supply-air connector to face the centroid of the room's air terminals.
// SOURCE:  ../knowledge/live-model/hvac-ducts.md § Placing equipment relative to a door, § Rotating equipment to face
//          a target direction
// STATUS:  living document — refine in place, don't fork a v2 file.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId roomId = ElementId.InvalidElementId;
FamilySymbol fcuSymbol = null; // e.g. resolve "STI_ME_FCU_Fan Coil Unit" via FilteredElementCollector
double heightAboveCeilingMm = 400; // FCU sits this far above the ceiling, in the void
double doorInsetMm = 500;          // 0 = leave at room center, no door-side shift
bool rotateToFaceTerminals = true;
// ---- END INPUTS ----

if (roomId == ElementId.InvalidElementId || fcuSymbol == null)
{
    return "Set roomId and fcuSymbol explicitly in INPUTS before running.";
}

var room = Document.GetElement(roomId) as Autodesk.Revit.DB.Architecture.Room;
if (room == null) return "roomId does not point to a valid Room.";

var level = Document.GetElement(room.LevelId) as Level;
var roomCenter = (room.Location as LocationPoint)?.Point ?? XYZ.Zero;

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
double ceilingHeightMm = ceiling?.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM)?.AsDouble() != null
    ? UnitUtils.ConvertFromInternalUnits(ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).AsDouble(), DisplayUnitType.DUT_MILLIMETERS)
    : 2400;

double zFt = UnitUtils.ConvertToInternalUnits(ceilingHeightMm + heightAboveCeilingMm, DisplayUnitType.DUT_MILLIMETERS);
XYZ placePt = new XYZ(roomCenter.X, roomCenter.Y, roomCenter.Z + zFt);

var sb = new System.Text.StringBuilder();

FamilyInstance fcu;
using (var t = new Transaction(Document, "AJ Tools - Place FCU"))
{
    t.Start();
    if (!fcuSymbol.IsActive) fcuSymbol.Activate();
    fcu = Document.Create.NewFamilyInstance(placePt, fcuSymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
    t.Commit();
}
sb.AppendLine($"Placed FCU at room center, {heightAboveCeilingMm}mm above {ceilingHeightMm}mm ceiling.");

// Door-side shift — perpendicular-to-wall axis only, tangential position stays wherever it already is.
if (doorInsetMm > 0)
{
    var phase = Document.Phases.get_Item(Document.Phases.Size - 1);
    var doors = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_Doors)
        .WhereElementIsNotElementType()
        .Cast<FamilyInstance>()
        .Where(d => d.get_ToRoom(phase)?.Id == room.Id || d.get_FromRoom(phase)?.Id == room.Id)
        .ToList();

    var door = doors.FirstOrDefault();
    if (door?.Host is Wall wall && wall.Location is LocationCurve lc)
    {
        var wallDir = (lc.Curve.GetEndPoint(1) - lc.Curve.GetEndPoint(0)).Normalize();
        var normal = new XYZ(-wallDir.Y, wallDir.X, 0);
        var doorPt = (door.Location as LocationPoint)?.Point ?? roomCenter;

        // Test which normal direction points into the room; flip if needed.
        var testPt = doorPt + normal * UnitUtils.ConvertToInternalUnits(200, DisplayUnitType.DUT_MILLIMETERS);
        if (!room.IsPointInRoom(new XYZ(testPt.X, testPt.Y, roomCenter.Z))) normal = -normal;

        double insetFt = UnitUtils.ConvertToInternalUnits(doorInsetMm, DisplayUnitType.DUT_MILLIMETERS);
        double tangentialComponent = (roomCenter - doorPt).DotProduct(wallDir);
        XYZ finalXY = doorPt + normal * insetFt + wallDir * tangentialComponent;
        XYZ newPt = new XYZ(finalXY.X, finalXY.Y, placePt.Z);

        using (var t = new Transaction(Document, "AJ Tools - Reposition FCU to Door"))
        {
            t.Start();
            ElementTransformUtils.MoveElement(Document, fcu.Id, newPt - placePt);
            t.Commit();
        }
        placePt = newPt;
        sb.AppendLine($"Shifted {doorInsetMm}mm toward the door wall (tangential position unchanged).");
    }
    else
    {
        sb.AppendLine("No door found for this room — skipped the door-side shift.");
    }
}

// Rotate the real supply-air connector to face the terminal centroid.
if (rotateToFaceTerminals)
{
    var terminals = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_DuctTerminal)
        .WhereElementIsNotElementType()
        .Cast<FamilyInstance>()
        .Where(fi =>
        {
            var loc = (fi.Location as LocationPoint)?.Point;
            if (loc == null) return false;
            var testPt = new XYZ(loc.X, loc.Y, roomCenter.Z);
            return room.IsPointInRoom(testPt);
        })
        .ToList();

    if (terminals.Count > 0)
    {
        var centroid = new XYZ(terminals.Average(t2 => (t2.Location as LocationPoint).Point.X),
                                terminals.Average(t2 => (t2.Location as LocationPoint).Point.Y), placePt.Z);

        var hvacConnectors = fcu.MEPModel?.ConnectorManager?.Connectors?.Cast<Connector>()
            .Where(c => c.Domain == Domain.DomainHvac && c.DuctSystemType == DuctSystemType.SupplyAir)
            .ToList();
        // Prefer the connector with an empty Description — the "Fresh Air" one is a decoy intake connector.
        var supplyConn = hvacConnectors?.FirstOrDefault(c => string.IsNullOrEmpty(c.Description))
            ?? hvacConnectors?.FirstOrDefault();

        if (supplyConn != null)
        {
            var currentDir = new XYZ(supplyConn.CoordinateSystem.BasisZ.X, supplyConn.CoordinateSystem.BasisZ.Y, 0);
            var targetDir = new XYZ(centroid.X - placePt.X, centroid.Y - placePt.Y, 0);
            double currentAngle = Math.Atan2(currentDir.Y, currentDir.X);
            double targetAngle = Math.Atan2(targetDir.Y, targetDir.X);
            double rotation = targetAngle - currentAngle;
            if (rotation > Math.PI) rotation -= 2 * Math.PI;
            if (rotation < -Math.PI) rotation += 2 * Math.PI;

            using (var t = new Transaction(Document, "AJ Tools - Rotate FCU to Terminals"))
            {
                t.Start();
                var axis = Line.CreateBound(placePt, placePt + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(Document, fcu.Id, axis, rotation);
                t.Commit();
            }
            sb.AppendLine($"Rotated FCU {Math.Round(rotation * 180 / Math.PI)} deg to face the terminal centroid.");
        }
        else
        {
            sb.AppendLine("No real supply-air HVAC connector found on this FCU family — skipped rotation.");
        }
    }
    else
    {
        sb.AppendLine("No terminals in this room yet — skipped rotation (nothing to face).");
    }
}

return sb.ToString();
