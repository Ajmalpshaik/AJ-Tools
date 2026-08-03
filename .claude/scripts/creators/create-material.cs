// ============================================================
// FRAGMENT (creator) — create-material.cs
// PURPOSE: Create one or more new Materials, setting color and transparency. Produces `elements` (the
//          new Material objects) for chaining into an action (e.g. report/select afterward).
// PRODUCES: elements (List<Element>), sb (StringBuilder)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
var materialsToCreate = new List<(string name, byte r, byte g, byte b, int transparencyPercent)> {
    ("New Material", 200, 200, 200, 0)
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var existingNames = new HashSet<string>(
    new FilteredElementCollector(Document).OfClass(typeof(Material)).Cast<Material>().Select(m => m.Name),
    StringComparer.OrdinalIgnoreCase);

int created = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Create Materials"))
{
    t.Start();
    try
    {
        foreach (var spec in materialsToCreate)
        {
            if (existingNames.Contains(spec.name)) { skipped++; continue; }

            var id = Material.Create(Document, spec.name);
            var mat = Document.GetElement(id) as Material;
            mat.Color = new Autodesk.Revit.DB.Color(spec.r, spec.g, spec.b);
            mat.Transparency = Math.Max(0, Math.Min(100, spec.transparencyPercent));
            elements.Add(mat);
            existingNames.Add(spec.name);
            created++;
        }
        t.Commit();
        sb.AppendLine($"Created {created} material(s), skipped {skipped} (name already exists).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to create materials — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to finish ----
