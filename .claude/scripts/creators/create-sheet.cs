// ============================================================
// FRAGMENT (creator) — create-sheet.cs
// PURPOSE: Create one or more new sheets with a chosen title block, setting sheet number and name.
// PRODUCES: elements (List<Element>) — the newly created ViewSheet(s), sb (StringBuilder)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose with an action fragment.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string titleBlockFamilyTypeName = null; // null = use the first available title block type in the project
var sheetsToCreate = new List<(string number, string name)> {
    ("A-101", "New Sheet")
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

FamilySymbol titleBlockType;
if (string.IsNullOrEmpty(titleBlockFamilyTypeName))
{
    titleBlockType = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_TitleBlocks)
        .WhereElementIsElementType()
        .Cast<FamilySymbol>()
        .FirstOrDefault();
}
else
{
    titleBlockType = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_TitleBlocks)
        .WhereElementIsElementType()
        .Cast<FamilySymbol>()
        .FirstOrDefault(f => f.Name.Equals(titleBlockFamilyTypeName, StringComparison.OrdinalIgnoreCase));
}

if (titleBlockType == null)
{
    sb.AppendLine("No title block type found" + (string.IsNullOrEmpty(titleBlockFamilyTypeName) ? " in the project." : $" named '{titleBlockFamilyTypeName}'."));
}
else
{
    int created = 0, skipped = 0;
    using (var t = new Transaction(Document, "AJ Tools - Create Sheets"))
    {
        t.Start();
        try
        {
            if (!titleBlockType.IsActive) { titleBlockType.Activate(); Document.Regenerate(); }

            foreach (var pair in sheetsToCreate)
            {
                var existing = new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                    .FirstOrDefault(s => s.SheetNumber.Equals(pair.number, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { skipped++; continue; }

                var sheet = ViewSheet.Create(Document, titleBlockType.Id);
                sheet.SheetNumber = pair.number;
                sheet.Name = pair.name;
                elements.Add(sheet);
                created++;
            }
            t.Commit();
            sb.AppendLine($"Created {created} sheet(s), skipped {skipped} (sheet number already exists).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create sheets — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with an action fragment below (e.g. action-report-parameters.cs), or add return sb.ToString(); to finish ----
