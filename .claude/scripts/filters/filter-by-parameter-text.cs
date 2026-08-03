// ============================================================
// FRAGMENT (filter) - filter-by-parameter-text.cs
// PURPOSE: Collect elements whose instance/type/family text matches a requested value. Use this for
//          "VCD", "family name contains", "type name is", "comments contain", "mark begins with", etc.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE - see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: AJ Tools Filter Pro parameter-value scan pattern and Autodesk parameter access guidance.
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
bool useCategoryFilter = true;
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctAccessory;
string parameterName = "Family and Type"; // special values: "Family", "Type", "Family and Type", or any parameter name
string matchText = "VCD";
string matchMode = "contains"; // contains, equals, begins, ends, notcontains, notequals
bool includeTypeParameters = true;
bool activeViewOnly = false;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

Func<Element, string> familyAndTypeText = e =>
{
    if (e is FamilyInstance fi && fi.Symbol != null)
        return $"{fi.Symbol.FamilyName} : {fi.Symbol.Name}";

    var type = Document.GetElement(e.GetTypeId()) as ElementType;
    if (type != null)
    {
        string family = type.FamilyName;
        if (string.IsNullOrWhiteSpace(family))
        {
            var famParam = type.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM)
                ?? type.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);
            family = famParam?.AsString();
        }

        return string.IsNullOrWhiteSpace(family) ? type.Name : $"{family} : {type.Name}";
    }

    return e.Name ?? "";
};

Func<Parameter, string> parameterToText = p =>
{
    if (p == null || !p.HasValue)
        return "";

    if (p.StorageType == StorageType.String)
        return p.AsString() ?? p.AsValueString() ?? "";

    if (p.StorageType == StorageType.Integer)
        return p.AsValueString() ?? p.AsInteger().ToString();

    if (p.StorageType == StorageType.Double)
        return p.AsValueString() ?? p.AsDouble().ToString("0.###");

    if (p.StorageType == StorageType.ElementId)
    {
        ElementId id = p.AsElementId();
        if (id == null || id == ElementId.InvalidElementId)
            return "";

        var refElement = Document.GetElement(id);
        return refElement?.Name ?? ("#" + id.IntegerValue);
    }

    return "";
};

Func<Element, string, string> getText = (e, name) =>
{
    if (string.Equals(name, "Family and Type", StringComparison.OrdinalIgnoreCase))
        return familyAndTypeText(e);

    if (string.Equals(name, "Family", StringComparison.OrdinalIgnoreCase))
    {
        if (e is FamilyInstance fi && fi.Symbol != null)
            return fi.Symbol.FamilyName ?? "";

        return (Document.GetElement(e.GetTypeId()) as ElementType)?.FamilyName ?? "";
    }

    if (string.Equals(name, "Type", StringComparison.OrdinalIgnoreCase))
    {
        if (e is FamilyInstance fi && fi.Symbol != null)
            return fi.Symbol.Name ?? "";

        return (Document.GetElement(e.GetTypeId()) as ElementType)?.Name ?? "";
    }

    Parameter p = e.LookupParameter(name);
    if ((p == null || !p.HasValue) && includeTypeParameters)
    {
        var type = Document.GetElement(e.GetTypeId()) as ElementType;
        p = type?.LookupParameter(name);
    }

    return parameterToText(p);
};

Func<string, string, string, bool> matches = (value, needle, mode) =>
{
    value = value ?? "";
    needle = needle ?? "";
    mode = (mode ?? "contains").Trim().ToLowerInvariant();

    if (mode == "equals") return string.Equals(value, needle, StringComparison.OrdinalIgnoreCase);
    if (mode == "begins") return value.StartsWith(needle, StringComparison.OrdinalIgnoreCase);
    if (mode == "ends") return value.EndsWith(needle, StringComparison.OrdinalIgnoreCase);
    if (mode == "notcontains") return value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0;
    if (mode == "notequals") return !string.Equals(value, needle, StringComparison.OrdinalIgnoreCase);
    return value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
};

var collector = activeViewOnly
    ? new FilteredElementCollector(Document, Document.ActiveView.Id)
    : new FilteredElementCollector(Document);

if (useCategoryFilter)
{
    collector.OfCategory(targetCategory);
}

IEnumerable<Element> query = collector.WhereElementIsNotElementType();

List<Element> elements = query
    .Where(e => matches(getText(e, parameterName), matchText, matchMode))
    .ToList();

string categoryLabel = useCategoryFilter ? targetCategory.ToString() : "all categories";
string scopeLabel = activeViewOnly ? $"active view '{Document.ActiveView.Name}'" : "whole model";
sb.AppendLine($"Filtered {elements.Count} element(s): {parameterName} {matchMode} '{matchText}', category: {categoryLabel}, scope: {scopeLabel}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
