// ============================================================
// FRAGMENT (action) — action-duplicate-views.cs
// PURPOSE: Duplicate each view in `elements` — plain duplicate, duplicate-with-detailing (keeps
//          annotations/details), or as a dependent view (stays linked to the parent's crop/changes).
// ASSUMES: elements (List<Element>) are View objects, sb (StringBuilder) exists.
// PRODUCES: newViewIds (List<ElementId>) — the freshly created view duplicates, for chaining.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string duplicateOption = "WithDetailing"; // "Duplicate" | "WithDetailing" | "AsDependent"
// ---- END INPUTS ----

ViewDuplicateOption option = duplicateOption == "AsDependent" ? ViewDuplicateOption.AsDependent
    : duplicateOption == "Duplicate" ? ViewDuplicateOption.Duplicate
    : ViewDuplicateOption.WithDetailing;

var newViewIds = new List<ElementId>();
int duplicated = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Duplicate Views"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            var view = el as View;
            if (view == null || !view.CanViewBeDuplicated(option)) { skipped++; continue; }
            var newId = view.Duplicate(option);
            newViewIds.Add(newId);
            duplicated++;
        }
        t.Commit();
        sb.AppendLine($"Duplicated {duplicated} view(s) ({duplicateOption}), skipped {skipped} (not duplicable this way).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to duplicate views — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
