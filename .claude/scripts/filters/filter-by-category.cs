// ============================================================
// FRAGMENT (filter) — filter-by-category.cs
// PURPOSE: Every instance of one category, optionally scoped to a level. The simplest filter — use
//          this when there's no family/size/room condition at all.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// Level matching tries several element-type-specific properties in order, since there is no single
// universal "get this element's level" API — Wall/Floor/FamilyInstance each store it differently.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
ElementId levelIdFilter = ElementId.InvalidElementId; // InvalidElementId = whole model, any level
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

Func<Element, ElementId> resolveLevelId = e =>
{
    if (e is Wall wall) return wall.LevelId;
    if (e.LevelId != ElementId.InvalidElementId) return e.LevelId;
    var p = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
    return p?.AsElementId() ?? ElementId.InvalidElementId;
};

var query = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .AsEnumerable();

if (levelIdFilter != ElementId.InvalidElementId)
{
    query = query.Where(e => resolveLevelId(e) == levelIdFilter);
}

List<Element> elements = query.ToList();
sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
