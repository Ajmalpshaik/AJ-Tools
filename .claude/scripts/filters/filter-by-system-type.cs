// ============================================================
// FRAGMENT (filter) — filter-by-system-type.cs
// PURPOSE: Every pipe/fitting/duct/duct-fitting whose MEP system name contains a filter string.
//          The system-name filter is ALWAYS an input — check glossary.md for Ajmal's word -> the real
//          Revit system-type name(s) (e.g. "refrigerant" -> anything containing "DXS").
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: ../knowledge/live-model/mep-trace.md § Tracing real MEP connectivity
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string systemNameContains = "DXS"; // e.g. "DXS" (refrigerant), "CDP" (condensate), "WSP" (water supply)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

List<Element> elements = new FilteredElementCollector(Document)
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

sb.AppendLine($"Filtered {elements.Count} element(s), system name contains '{systemNameContains}'.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
