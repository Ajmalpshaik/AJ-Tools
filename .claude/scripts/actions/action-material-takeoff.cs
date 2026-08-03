// ============================================================
// FRAGMENT (action) — action-material-takeoff.cs
// PURPOSE: Sum material area/volume across every element in `elements`, grouped by material name —
//          a quantities/takeoff report. Read-only, no transaction needed.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// SOURCE: capability confirmed as a common convergent feature across independent Revit-MCP projects
//         (an-external-project, an external repository) — written independently for this project's own
//         conventions, no code taken.
// ============================================================

var totals = new Dictionary<string, (double areaSqm, double volumeCum)>();
int skipped = 0;

foreach (var e in elements)
{
    ICollection<ElementId> materialIds;
    try
    {
        materialIds = e.GetMaterialIds(false); // false = real materials, not paint
    }
    catch
    {
        skipped++;
        continue;
    }

    if (materialIds == null || materialIds.Count == 0) { skipped++; continue; }

    foreach (var matId in materialIds)
    {
        var material = Document.GetElement(matId) as Material;
        string name = material?.Name ?? "Unknown";

        double areaFt2 = 0, volumeFt3 = 0;
        try { areaFt2 = e.GetMaterialArea(matId, false); } catch { }
        try { volumeFt3 = e.GetMaterialVolume(matId); } catch { }

        double areaSqm = UnitUtils.ConvertFromInternalUnits(areaFt2, DisplayUnitType.DUT_SQUARE_METERS);
        double volumeCum = UnitUtils.ConvertFromInternalUnits(volumeFt3, DisplayUnitType.DUT_CUBIC_METERS);

        totals.TryGetValue(name, out var existing);
        totals[name] = (existing.areaSqm + areaSqm, existing.volumeCum + volumeCum);
    }
}

sb.AppendLine($"Material takeoff across {elements.Count} element(s) ({skipped} skipped, no material data):");
sb.AppendLine("Material | Area (sqm) | Volume (cum)");
sb.AppendLine("--- | --- | ---");
foreach (var kv in totals.OrderByDescending(kv => kv.Value.volumeCum))
{
    sb.AppendLine($"{kv.Key} | {kv.Value.areaSqm:F2} | {kv.Value.volumeCum:F2}");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
