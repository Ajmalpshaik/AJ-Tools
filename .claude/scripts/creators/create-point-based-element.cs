// ============================================================
// FRAGMENT (creator) — create-point-based-element.cs
// PURPOSE: Place a family instance (door, window, piece of equipment, furniture, anything
//          point-placed) at one or more points on a level. Matches Ajmal's own past request shape
//          ("add N of X on level Y").
// PRODUCES: elements (List<Element>, the newly placed instance(s)), sb (StringBuilder, summary)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose. A "creator" fills the same role
//          as a filter — it produces `elements` — so any action fragment can be appended after it.
// ============================================================
// Every point and level is a per-request input — never a default. `StructuralType` must be fully
// qualified in this script context (bare `StructuralType` fails to compile — see ../knowledge/live-model/core.md).

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string familyNameContains = "";      // e.g. "STI_ME_FCU" — leave blank to match any family
string typeNameContains = "";        // narrow to a specific type within the family, or leave blank
ElementId levelId = ElementId.InvalidElementId;
List<(double xMm, double yMm, double zMm)> pointsMm = new List<(double, double, double)>
{
    (0, 0, 0)
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>();

if (levelId == ElementId.InvalidElementId)
{
    sb.AppendLine("No levelId set — nothing placed.");
}
else
{
    var symbol = new FilteredElementCollector(Document)
        .OfClass(typeof(FamilySymbol))
        .Cast<FamilySymbol>()
        .FirstOrDefault(fs =>
            (string.IsNullOrEmpty(familyNameContains) || fs.Family.Name.IndexOf(familyNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
            && (string.IsNullOrEmpty(typeNameContains) || fs.Name.IndexOf(typeNameContains, StringComparison.OrdinalIgnoreCase) >= 0));

    if (symbol == null)
    {
        sb.AppendLine($"No FamilySymbol found matching family '{familyNameContains}' / type '{typeNameContains}' — nothing placed.");
    }
    else
    {
        var level = Document.GetElement(levelId) as Level;

        using (var t = new Transaction(Document, "AJ Tools - Place Elements"))
        {
            t.Start();
            try
            {
                if (!symbol.IsActive) symbol.Activate();
                foreach (var (xMm, yMm, zMm) in pointsMm)
                {
                    var pt = new XYZ(
                        UnitUtils.ConvertToInternalUnits(xMm, DisplayUnitType.DUT_MILLIMETERS),
                        UnitUtils.ConvertToInternalUnits(yMm, DisplayUnitType.DUT_MILLIMETERS),
                        UnitUtils.ConvertToInternalUnits(zMm, DisplayUnitType.DUT_MILLIMETERS));
                    var fi = Document.Create.NewFamilyInstance(pt, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    elements.Add(fi);
                }
                t.Commit();
                sb.AppendLine($"Placed {elements.Count} instance(s) of '{symbol.Family.Name} : {symbol.Name}' on level '{level?.Name}'.");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to place elements — rolled back, nothing changed. Reason: {ex.Message}");
                elements = new List<Element>();
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
