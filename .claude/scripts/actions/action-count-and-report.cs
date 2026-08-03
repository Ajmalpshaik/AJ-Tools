// ============================================================
// FRAGMENT (action) — action-count-and-report.cs
// PURPOSE: Report on `elements` — bare count (default, per reply-style.md) or a size-breakdown table
//          when asked for sizes. Read-only, no transaction needed. If Ajmal asked about one specific
//          dimension (height, diameter...), set preferredParamName so that's the one reported, not
//          whichever generic fallback parameter happens to be found first.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool wantBreakdownTable = false; // true only when Ajmal actually asked for sizes/breakdown
// If Ajmal asked about ONE specific dimension (e.g. "what height"), put it here so it's tried first —
// otherwise this list is just a generic fallback order, and picking "Width" over "Height" first can
// silently answer the wrong dimension whenever a duct/pipe isn't square (caught 2026-07-09: an 8-duct
// test set was 300x300mm, so Width-first happened to still show the right number by coincidence).
string preferredParamName = null; // e.g. "Height", "Diameter" — leave null to use the fallback order below
string[] sizeParamNames = { "Width", "Height", "Size", "Nominal Diameter", "Diameter" };
// ---- END INPUTS ----

sb.AppendLine($"Count: {elements.Count}");

if (wantBreakdownTable && elements.Count > 0)
{
    var orderedParamNames = preferredParamName != null
        ? new[] { preferredParamName }.Concat(sizeParamNames.Where(n => n != preferredParamName)).ToArray()
        : sizeParamNames;

    var groups = new Dictionary<string, int>();
    foreach (var e in elements)
    {
        string sizeLabel = "unknown size";
        foreach (var pName in orderedParamNames)
        {
            var p = e.LookupParameter(pName);
            if (p != null && p.StorageType == StorageType.Double)
            {
                double mm = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), DisplayUnitType.DUT_MILLIMETERS);
                sizeLabel = $"{Math.Round(mm)}mm ({pName})";
                break;
            }
        }
        groups.TryGetValue(sizeLabel, out int existing);
        groups[sizeLabel] = existing + 1;
    }

    sb.AppendLine("Size (mm) | Qty");
    sb.AppendLine("--- | ---");
    foreach (var kv in groups.OrderByDescending(kv => kv.Value))
    {
        sb.AppendLine($"{kv.Key} | {kv.Value}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
