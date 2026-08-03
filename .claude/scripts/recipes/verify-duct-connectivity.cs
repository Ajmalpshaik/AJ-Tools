// ============================================================
// SCRIPT: verify-duct-connectivity.cs
// PURPOSE: Trace every terminal's full connector chain out to its FCU (riser -> elbow -> branch ->
//          takeoff -> main trunk -> FCU), reporting exactly where any silent break is. NEVER stops at
//          the terminal's own IsConnected flag - that only reflects the local link, not the full path.
// SOURCE:  ../knowledge/live-model/hvac-ducts.md (orphan-recovery trace technique, written for the trunk-slicing bug)
// STATUS:  living document - refine in place, don't fork a v2 file.
// ============================================================
// Read-only / diagnostic - this script does NOT fix anything it finds broken.
//
// GOTCHA (2026-07-17): this script's "reachedFcu" only counts a chain as OK if it hits an
// OST_MechanicalEquipment element. If zero FCUs are placed in the model yet (main duct/branches
// built but ajtools-hvac-duct-routing's FCU-placement step not run), EVERY terminal reports
// "broken" - that is a false alarm, not a real connectivity problem. Always check the FCU
// (Mechanical Equipment) count first; if it's 0, don't report "N broken" as fact - hand-trace one
// chain instead (walk AllRefs hop by hop, print IsConnected/category per hop) to see if the real
// duct/fitting network is actually intact.

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
ElementId roomId = ElementId.InvalidElementId; // ElementId.InvalidElementId = whole model
int maxHops = 60;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

IEnumerable<FamilyInstance> terminalQuery = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctTerminal)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>();

if (roomId != ElementId.InvalidElementId)
{
    var room = Document.GetElement(roomId) as Autodesk.Revit.DB.Architecture.Room;
    if (room == null) return "roomId does not point to a valid Room.";

    terminalQuery = terminalQuery.Where(fi =>
    {
        var loc = (fi.Location as LocationPoint)?.Point;
        if (loc == null) return false;
        var testPt = new XYZ(loc.X, loc.Y, (room.Location as LocationPoint)?.Point.Z ?? loc.Z);
        return room.IsPointInRoom(testPt);
    });
}

var terminals = terminalQuery.ToList();
sb.AppendLine($"Checking {terminals.Count} terminal(s).");

int okCount = 0, brokenCount = 0;

var mechanicalEquipmentCategory = Category.GetCategory(Document, BuiltInCategory.OST_MechanicalEquipment);
ElementId mechanicalEquipmentCategoryId = mechanicalEquipmentCategory?.Id ?? ElementId.InvalidElementId;

Func<Element, IEnumerable<Connector>> getConnectors = e =>
{
    var mgr = (e as MEPCurve)?.ConnectorManager
        ?? (e is FamilyInstance fi ? fi.MEPModel?.ConnectorManager : null);
    return mgr == null ? Enumerable.Empty<Connector>() : mgr.Connectors.Cast<Connector>();
};

Func<Connector, string> connectorKey = c => $"{c.Owner.Id}:{c.Id}";

foreach (var term in terminals)
{
    var terminalConnectorSet = term.MEPModel?.ConnectorManager?.Connectors;
    var termConn = terminalConnectorSet == null
        ? null
        : terminalConnectorSet.Cast<Connector>().FirstOrDefault(c => c.Domain == Domain.DomainHvac);
    if (termConn == null)
    {
        sb.AppendLine($"  Terminal {term.Id}: no HVAC connector found - anomaly.");
        continue;
    }

    bool reachedFcu = false;
    Element breakElem = term;
    int hops = 0;

    var queue = new Queue<Tuple<Connector, Element, int>>();
    var visited = new HashSet<string>();
    queue.Enqueue(Tuple.Create(termConn, (Element)term, 0));
    visited.Add(connectorKey(termConn));

    while (queue.Count > 0)
    {
        var item = queue.Dequeue();
        var current = item.Item1;
        var currentElem = item.Item2;
        int depth = item.Item3;
        hops = Math.Max(hops, depth);

        if (depth >= maxHops)
        {
            breakElem = currentElem;
            continue;
        }

        var refs = current.AllRefs?.Cast<Connector>()
            .Where(r => r.Owner != null && r.Owner.Id != currentElem.Id)
            .ToList();

        if (refs == null || refs.Count == 0)
        {
            breakElem = currentElem;
            continue;
        }

        foreach (var next in refs)
        {
            var nextElem = next.Owner;
            if (nextElem == null) continue;

            if (nextElem.Category?.Id.Equals(mechanicalEquipmentCategoryId) == true)
            {
                reachedFcu = true;
                hops = Math.Max(hops, depth + 1);
                break;
            }

            var nextConnectors = getConnectors(nextElem)
                .Where(c => c.Id != next.Id)
                .ToList();

            if (nextConnectors.Count == 0)
            {
                breakElem = nextElem;
                hops = Math.Max(hops, depth + 1);
                continue;
            }

            foreach (var outConn in nextConnectors)
            {
                var key = connectorKey(outConn);
                if (!visited.Add(key)) continue;
                queue.Enqueue(Tuple.Create(outConn, nextElem, depth + 1));
            }
        }

        if (reachedFcu) break;
    }

    if (reachedFcu)
    {
        okCount++;
    }
    else
    {
        brokenCount++;
        string label = term.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? term.Id.ToString();
        sb.AppendLine($"  BROKEN - terminal {label}: no connector path reached Mechanical Equipment; last checked element Id {breakElem?.Id} ({breakElem?.Category?.Name}) after {hops} hop(s).");
    }
}

sb.AppendLine($"Result: {okCount}/{terminals.Count} fully connected to an FCU, {brokenCount} broken.");
sb.AppendLine("Never trust IsConnected on the terminal alone - this trace is the reliable check.");
return sb.ToString();
