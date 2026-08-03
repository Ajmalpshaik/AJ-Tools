// ============================================================
// FRAGMENT (creator) — create-levels.cs
// PURPOSE: Batch-create Levels, either at even spacing from a start elevation, or at an explicit list
//          of elevations. Matches Ajmal's own past request shape ("create levels up to N").
// PRODUCES: elements (List<Element>, the newly created Level(s)), sb (StringBuilder, summary appended)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose. A "creator" fills the same role
//          as a filter — it produces `elements` — so any action fragment (rename, color, report) can
//          be appended after it exactly like it would after a filter.
// ============================================================
// Every elevation/spacing number is a per-request input — never a default.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool useExplicitElevations = false;
List<double> explicitElevationsMm = new List<double> { 0, 3000, 6000 }; // used when useExplicitElevations = true
int count = 3;                 // used when useExplicitElevations = false
double startElevationMm = 0;
double spacingMm = 3000;
string namePrefix = "Level ";  // level N gets name "{namePrefix}{N}" unless it collides with an existing name
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<double> elevationsMm = useExplicitElevations
    ? explicitElevationsMm
    : Enumerable.Range(0, count).Select(i => startElevationMm + i * spacingMm).ToList();

var existingNames = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .Select(l => l.Name)
    .ToHashSet();

List<Element> elements = new List<Element>();

using (var t = new Transaction(Document, "AJ Tools - Create Levels"))
{
    t.Start();
    try
    {
        int n = 1;
        foreach (var elevMm in elevationsMm)
        {
            double elevFt = UnitUtils.ConvertToInternalUnits(elevMm, DisplayUnitType.DUT_MILLIMETERS);
            var level = Level.Create(Document, elevFt);

            string candidateName = $"{namePrefix}{n}";
            while (existingNames.Contains(candidateName)) { n++; candidateName = $"{namePrefix}{n}"; }
            level.Name = candidateName;
            existingNames.Add(candidateName);

            elements.Add(level);
            n++;
        }
        t.Commit();
        sb.AppendLine($"Created {elements.Count} level(s) at elevations (mm): {string.Join(", ", elevationsMm)}.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to create levels — rolled back, nothing changed. Reason: {ex.Message}");
        elements = new List<Element>();
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
