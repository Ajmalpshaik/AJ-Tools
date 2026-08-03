// ============================================================
// FRAGMENT (action) — action-change-element-type.cs
// PURPOSE: Bulk-swap every element in `elements` from its current type to a different named type within
//          the SAME family — e.g. change every "600x300" duct fitting instance to "600x600", or every
//          door instance from one type to another. An element whose family has no type by that name is
//          skipped, never guessed at.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with Ajmal before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetTypeName = "New Type Name";
// ---- END INPUTS ----

int changed = 0, skipped = 0;
var typeCache = new Dictionary<ElementId, ElementId>(); // current type Id -> resolved target type Id

using (var t = new Transaction(Document, "AJ Tools - Change Element Type"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var currentTypeId = e.GetTypeId();
            if (currentTypeId == ElementId.InvalidElementId) { skipped++; continue; }

            if (!typeCache.TryGetValue(currentTypeId, out var targetTypeId))
            {
                var currentType = Document.GetElement(currentTypeId) as ElementType;
                ElementType match = null;

                if (currentType is FamilySymbol currentSymbol)
                {
                    match = new FilteredElementCollector(Document)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(fs => fs.Family.Id == currentSymbol.Family.Id
                            && fs.Name.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase));
                }
                else if (currentType != null)
                {
                    // System-family types (duct/pipe/wall types, etc.) — match within the same type class.
                    match = new FilteredElementCollector(Document)
                        .OfClass(currentType.GetType())
                        .Cast<ElementType>()
                        .FirstOrDefault(et => et.FamilyName == currentType.FamilyName
                            && et.Name.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase));
                }

                targetTypeId = match?.Id ?? ElementId.InvalidElementId;
                typeCache[currentTypeId] = targetTypeId;
            }

            if (targetTypeId == ElementId.InvalidElementId || targetTypeId == currentTypeId) { skipped++; continue; }

            try
            {
                e.ChangeTypeId(targetTypeId);
                changed++;
            }
            catch { skipped++; } // some type changes aren't valid for a given instance — skip, don't fail the batch
        }
        t.Commit();
        sb.AppendLine($"Changed {changed} element(s) to type '{targetTypeName}', skipped {skipped} (target type not found in that family, already that type, or change not supported for that instance).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to change element type — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
