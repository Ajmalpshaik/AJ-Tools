// ============================================================
// FRAGMENT (filter) — filter-by-phase.cs
// PURPOSE: Elements whose Phase Created and/or Phase Demolished matches a named project Phase — e.g.
//          "everything added in Phase 2", "what's being demolished in Phase 1". Optionally narrowed to
//          one or more categories.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string createdPhaseName = null;    // e.g. "Phase 2" — leave null to not filter on Phase Created
string demolishedPhaseName = null; // e.g. "Phase 1" — leave null to not filter on Phase Demolished
BuiltInCategory[] categoryScope = new BuiltInCategory[0]; // empty = every model category
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>();

if (string.IsNullOrEmpty(createdPhaseName) && string.IsNullOrEmpty(demolishedPhaseName))
{
    sb.AppendLine("No phase specified — set createdPhaseName and/or demolishedPhaseName.");
}
else
{
    var allPhases = new FilteredElementCollector(Document).OfClass(typeof(Phase)).Cast<Phase>().ToList();
    Phase createdPhase = string.IsNullOrEmpty(createdPhaseName) ? null :
        allPhases.FirstOrDefault(ph => ph.Name.Equals(createdPhaseName, StringComparison.OrdinalIgnoreCase));
    Phase demolishedPhase = string.IsNullOrEmpty(demolishedPhaseName) ? null :
        allPhases.FirstOrDefault(ph => ph.Name.Equals(demolishedPhaseName, StringComparison.OrdinalIgnoreCase));

    if (!string.IsNullOrEmpty(createdPhaseName) && createdPhase == null)
        sb.AppendLine($"Phase '{createdPhaseName}' not found. Available: {string.Join(", ", allPhases.Select(p => p.Name))}");
    else if (!string.IsNullOrEmpty(demolishedPhaseName) && demolishedPhase == null)
        sb.AppendLine($"Phase '{demolishedPhaseName}' not found. Available: {string.Join(", ", allPhases.Select(p => p.Name))}");
    else
    {
        IEnumerable<Element> query;
        if (categoryScope.Length > 0)
        {
            var categoryIds = categoryScope.Select(c => new ElementId(c)).ToList();
            query = new FilteredElementCollector(Document)
                .WherePasses(new ElementMulticategoryFilter(categoryIds))
                .WhereElementIsNotElementType();
        }
        else
        {
            query = new FilteredElementCollector(Document).WhereElementIsNotElementType();
        }

        elements = query.Where(e =>
        {
            if (e == null) return false;
            try
            {
                bool createdOk = createdPhase == null || e.CreatedPhaseId == createdPhase.Id;
                bool demolishedOk = demolishedPhase == null || e.DemolishedPhaseId == demolishedPhase.Id;
                return createdOk && demolishedOk;
            }
            catch { return false; } // some element types don't support phasing — skip, don't error
        }).ToList();

        string categoryLabel = categoryScope.Length == 0 ? "all categories" : string.Join(", ", categoryScope.Select(c => c.ToString()));
        sb.AppendLine($"Filtered {elements.Count} element(s), categories: {categoryLabel}, Created={createdPhaseName ?? "(any)"}, Demolished={demolishedPhaseName ?? "(any)"}.");
    }
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
