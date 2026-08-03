// ============================================================
// FRAGMENT (filter) — filter-by-region.cs
// PURPOSE: Every instance of a category whose bounding box intersects a given 3D region — for
//          "elements in this area" when there's no Room to filter by (or the area spans multiple
//          rooms/outdoors). Coordinates in mm, converted to Revit's internal feet.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: BoundingBoxIntersectsFilter usage adapted from
//         an external repository's AIElementFilterEventHandler — only
//         the technique was taken, not any code.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
double minXmm = 0, minYmm = 0, minZmm = 0;
double maxXmm = 5000, maxYmm = 5000, maxZmm = 3000;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

XYZ minPt = new XYZ(
    UnitUtils.ConvertToInternalUnits(minXmm, DisplayUnitType.DUT_MILLIMETERS),
    UnitUtils.ConvertToInternalUnits(minYmm, DisplayUnitType.DUT_MILLIMETERS),
    UnitUtils.ConvertToInternalUnits(minZmm, DisplayUnitType.DUT_MILLIMETERS));
XYZ maxPt = new XYZ(
    UnitUtils.ConvertToInternalUnits(maxXmm, DisplayUnitType.DUT_MILLIMETERS),
    UnitUtils.ConvertToInternalUnits(maxYmm, DisplayUnitType.DUT_MILLIMETERS),
    UnitUtils.ConvertToInternalUnits(maxZmm, DisplayUnitType.DUT_MILLIMETERS));

var outline = new Outline(minPt, maxPt);
var bboxFilter = new BoundingBoxIntersectsFilter(outline);

List<Element> elements = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .WherePasses(bboxFilter)
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory} intersecting the given region.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
