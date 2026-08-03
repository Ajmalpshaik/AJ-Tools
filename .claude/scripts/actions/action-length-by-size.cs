// ============================================================
// FRAGMENT (action) — action-length-by-size.cs
// PURPOSE: Report count AND total length per size group for linear MEP elements (ducts, pipes, cable
//          trays — anything with a "Size" string parameter and a Length parameter). Different from
//          action-count-and-report.cs's breakdown table, which counts per size but doesn't sum length.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// RBS_CALCULATED_SIZE works for ducts and pipes (string like "300x150" or "DN200"). Cable trays use the
// same BuiltInParameter name too. CURVE_ELEM_LENGTH works for any curve-based MEP element.
BuiltInParameter sizeParam = BuiltInParameter.RBS_CALCULATED_SIZE;
BuiltInParameter lengthParam = BuiltInParameter.CURVE_ELEM_LENGTH;
// ---- END INPUTS ----

var groups = new Dictionary<string, Tuple<int, double>>(); // size -> (count, totalLengthMm)

foreach (var e in elements)
{
    var sp = e.get_Parameter(sizeParam);
    var lp = e.get_Parameter(lengthParam);
    string size = (sp != null && sp.HasValue) ? sp.AsString() : "unknown";
    double lenMm = (lp != null && lp.HasValue) ? UnitUtils.ConvertFromInternalUnits(lp.AsDouble(), DisplayUnitType.DUT_MILLIMETERS) : 0;

    if (!groups.ContainsKey(size)) groups[size] = Tuple.Create(0, 0.0);
    var cur = groups[size];
    groups[size] = Tuple.Create(cur.Item1 + 1, cur.Item2 + lenMm);
}

// Sort key: parse the leading number(s) out of the size string ("450x250" -> 450,250; "250ø" -> 250,250)
// so rows read smallest-to-largest like a Revit schedule, not by qty/length. Ajmal's standing rule
// (2026-07-18, see reply-style.md) — never sort a size breakdown by qty or length.
Func<string, Tuple<double, double>> sizeSortKey = s =>
{
    var parts = s.Replace("ø", "").Split('x');
    double a = 0, b = 0;
    double.TryParse(parts[0], out a);
    if (parts.Length > 1) double.TryParse(parts[1], out b); else b = a;
    return Tuple.Create(a, b);
};

sb.AppendLine("Total: " + elements.Count);
sb.AppendLine("Size (mm) | Qty | Total Length (m)");
sb.AppendLine("--- | --- | ---");
double grandTotalM = 0;
foreach (var kv in groups.OrderBy(kv => sizeSortKey(kv.Key).Item1).ThenBy(kv => sizeSortKey(kv.Key).Item2))
{
    double totalM = kv.Value.Item2 / 1000.0;
    grandTotalM += totalM;
    sb.AppendLine($"{kv.Key} | {kv.Value.Item1} | {Math.Round(totalM, 2)}");
}
sb.AppendLine("--- | --- | ---");
sb.AppendLine($"TOTAL | {elements.Count} | {Math.Round(grandTotalM, 2)}");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
