// ============================================================
// FRAGMENT (filter) — filter-by-category-and-numeric-param.cs
// PURPOSE: One category, narrowed to instances where a numeric parameter matches a comparison against
//          an mm value. This is the "500mm height duct" filter — the general case, not duct-specific;
//          point it at any category + parameter (duct Height, pipe Diameter, wall Width, etc).
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// Ajmal speaks in mm; Revit's internal API is feet — this fragment converts explicitly.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
string parameterName = "Height"; // works with BuiltInParameter.RBS_CURVE_HEIGHT_PARAM's display name;
                                  // swap to "Width", "Diameter", etc. as needed — LookupParameter matches by name
double valueMm = 500;
// Comparison: "eq" (within toleranceMm), "gte", "lte", "between"
string comparison = "eq";
double valueMaxMm = 0; // only used when comparison == "between" — the upper bound
double toleranceMm = 1; // only used when comparison == "eq"
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

double valueFt = UnitUtils.ConvertToInternalUnits(valueMm, DisplayUnitType.DUT_MILLIMETERS);
double toleranceFt = UnitUtils.ConvertToInternalUnits(toleranceMm, DisplayUnitType.DUT_MILLIMETERS);
double valueMaxFt = UnitUtils.ConvertToInternalUnits(valueMaxMm, DisplayUnitType.DUT_MILLIMETERS);

List<Element> elements = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .Where(e =>
    {
        var p = e.LookupParameter(parameterName);
        if (p == null || p.StorageType != StorageType.Double) return false;
        double v = p.AsDouble();
        switch (comparison)
        {
            case "gte": return v >= valueFt;
            case "lte": return v <= valueFt;
            case "between": return v >= valueFt && v <= valueMaxFt;
            default: return Math.Abs(v - valueFt) <= toleranceFt; // "eq"
        }
    })
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s) where {parameterName} {comparison} {valueMm}mm.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
