// ============================================================
// FRAGMENT (action) - action-report-parameters.cs
// PURPOSE: Report selected parameter values for `elements` as a small table. Use this when Ajmal asks
//          "what are their sizes/levels/marks/families/system names" and the answer needs more than
//          a count.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE - see .claude/scripts/README.md for how to compose.
// SOURCE: AJ Tools Filter Pro parameter display pattern and Autodesk parameter access guidance.
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
string[] parameterNames = { "Family and Type", "Level", "Mark" };
int maxRows = 50;
bool includeElementId = true;
bool includeCategory = true;
bool includeTypeParameters = true;
// ---- END INPUTS ----

Func<string, string> cleanCell = value =>
{
    value = value ?? "";
    return value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();
};

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

Func<Element, string, string> getValue = (e, name) =>
{
    if (string.Equals(name, "ElementId", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase))
        return e.Id.IntegerValue.ToString();

    if (string.Equals(name, "Category", StringComparison.OrdinalIgnoreCase))
        return e.Category?.Name ?? "";

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

    if (string.Equals(name, "Level", StringComparison.OrdinalIgnoreCase))
    {
        ElementId levelId = e.LevelId;
        if (levelId == ElementId.InvalidElementId)
        {
            var levelParam = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            levelId = levelParam?.AsElementId() ?? ElementId.InvalidElementId;
        }

        return levelId == ElementId.InvalidElementId ? "" : Document.GetElement(levelId)?.Name ?? "";
    }

    Parameter p = e.LookupParameter(name);
    if ((p == null || !p.HasValue) && includeTypeParameters)
    {
        var type = Document.GetElement(e.GetTypeId()) as ElementType;
        p = type?.LookupParameter(name);
    }

    return parameterToText(p);
};

var headers = new List<string>();
if (includeElementId) headers.Add("Id");
if (includeCategory) headers.Add("Category");
headers.AddRange(parameterNames);

sb.AppendLine($"Showing {Math.Min(elements.Count, maxRows)} of {elements.Count} element(s).");
sb.AppendLine(string.Join(" | ", headers.Select(cleanCell)));
sb.AppendLine(string.Join(" | ", headers.Select(h => "---")));

foreach (var e in elements.Take(maxRows))
{
    var row = new List<string>();
    foreach (string h in headers)
    {
        row.Add(cleanCell(getValue(e, h)));
    }

    sb.AppendLine(string.Join(" | ", row));
}

if (elements.Count > maxRows)
{
    sb.AppendLine($"... {elements.Count - maxRows} more element(s) not shown. Increase maxRows only if Ajmal asks for the full table.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
