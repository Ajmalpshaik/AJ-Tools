// ============================================================
// SCRIPT: trace-mep-circuits.cs
// PURPOSE: Trace real physical MEP circuits for a filtered pipe/duct system type, when tags/naming
//          and Connector.IsConnected can't be trusted. Bulk-clusters the whole filtered set at once
//          (not one named path at a time), finds each circuit's open ends, and matches those to the
//          nearest real equipment.
// SOURCE:  ../knowledge/live-model/mep-trace.md § Tracing real MEP connectivity (when tags/naming can't be trusted)
// STATUS:  living document — refine in place, don't fork a v2 file.
// ============================================================
// The system-name filter is ALWAYS an input — never hardcode it. Check glossary.md for the mapping
// from Ajmal's word to the real Revit system-type name(s) (e.g. "refrigerant" -> anything containing "DXS").

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string systemNameContains = "DXS";  // e.g. "DXS" (refrigerant), "CDP" (condensate), "WSP" (water supply)
double clusterToleranceFt = UnitUtils.ConvertToInternalUnits(50, DisplayUnitType.DUT_MILLIMETERS); // ~50mm worked well
int maxHops = 60; // guard against a runaway geometric coincidence that isn't a real path
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

// Step 1 — collect every pipe + fitting whose system name/type matches the filter.
var candidates = new FilteredElementCollector(Document)
    .WhereElementIsNotElementType()
    .OfCategory(BuiltInCategory.OST_PipeCurves)
    .UnionWith(new FilteredElementCollector(Document).WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_PipeFitting))
    .UnionWith(new FilteredElementCollector(Document).WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_DuctCurves))
    .UnionWith(new FilteredElementCollector(Document).WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_DuctFitting))
    .Where(e =>
    {
        var sysParam = e.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM) ?? e.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
        var name = sysParam?.AsString() ?? sysParam?.AsValueString() ?? "";
        return name.IndexOf(systemNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
    })
    .ToList();

sb.AppendLine($"Collected {candidates.Count} element(s) matching system filter '{systemNameContains}'.");
if (candidates.Count == 0) return sb.ToString();

// Gather all connectors per element up front.
var elemConnectors = new Dictionary<ElementId, List<Connector>>();
foreach (var e in candidates)
{
    var mgr = (e as MEPCurve)?.ConnectorManager
        ?? (e is FamilyInstance fi ? fi.MEPModel?.ConnectorManager : null);
    if (mgr == null) continue;
    elemConnectors[e.Id] = mgr.Connectors.Cast<Connector>().ToList();
}

// Step 2 — bulk-cluster by matching connector positions within tolerance (fallback path; try the
// fast path first in a real run by sampling Connector.IsConnected within one MEPSystem — omitted here
// for brevity, see ../knowledge/live-model/mep-trace.md for that shortcut).
var parent = elemConnectors.Keys.ToDictionary(id => id, id => id); // union-find
Func<ElementId, ElementId> find = null;
find = id => parent[id] == id ? id : (parent[id] = find(parent[id]));
Action<ElementId, ElementId> union = (a, b) => { var ra = find(a); var rb = find(b); if (ra != rb) parent[ra] = rb; };

var allIds = elemConnectors.Keys.ToList();
for (int i = 0; i < allIds.Count; i++)
{
    for (int j = i + 1; j < allIds.Count; j++)
    {
        bool touches = elemConnectors[allIds[i]].Any(c1 =>
            elemConnectors[allIds[j]].Any(c2 => c1.Origin.DistanceTo(c2.Origin) <= clusterToleranceFt));
        if (touches) union(allIds[i], allIds[j]);
    }
}

var circuits = allIds.GroupBy(id => find(id)).ToList();
sb.AppendLine($"Grouped into {circuits.Count} physical circuit(s).");

// Step 3+4 — for each circuit, find open ends and match to nearest equipment.
var equipment = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
    .WhereElementIsNotElementType()
    .ToList();

int idx = 0;
foreach (var circuit in circuits)
{
    idx++;
    var members = circuit.ToList();
    var openConnectors = new List<Connector>();
    foreach (var id in members)
    {
        foreach (var c in elemConnectors[id])
        {
            bool hasNeighborInCircuit = members.Any(otherId => otherId != id &&
                elemConnectors[otherId].Any(oc => oc.Origin.DistanceTo(c.Origin) <= clusterToleranceFt));
            if (!hasNeighborInCircuit) openConnectors.Add(c);
        }
    }

    sb.AppendLine($"Circuit {idx}: {members.Count} element(s), {openConnectors.Count} open end(s).");
    foreach (var oc in openConnectors)
    {
        Element nearestEquip = null;
        double best = double.MaxValue;
        foreach (var eq in equipment)
        {
            var mepModel = (eq as FamilyInstance)?.MEPModel;
            var eqConnectors = mepModel?.ConnectorManager?.Connectors?.Cast<Connector>().ToList();
            double dist;
            if (eqConnectors != null && eqConnectors.Any())
            {
                dist = eqConnectors.Min(ec => ec.Origin.DistanceTo(oc.Origin));
            }
            else
            {
                // No connectors on this equipment (common for outdoor/condensing units) — fall back to bbox distance.
                var bbox = eq.get_BoundingBox(null);
                if (bbox == null) continue;
                var center = (bbox.Min + bbox.Max) * 0.5;
                dist = center.DistanceTo(oc.Origin);
            }
            if (dist < best) { best = dist; nearestEquip = eq; }
        }
        string equipName = nearestEquip?.LookupParameter("Equipment Tag")?.AsString()
            ?? nearestEquip?.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()
            ?? nearestEquip?.Name ?? "unknown";
        sb.AppendLine($"  Open end near {equipName} (Id {nearestEquip?.Id}), distance {UnitUtils.ConvertFromInternalUnits(best, DisplayUnitType.DUT_MILLIMETERS):F0}mm.");
    }
}

sb.AppendLine("Verify each pairing before reporting as fact — check glossary.md for an already-confirmed pattern first.");
return sb.ToString();
