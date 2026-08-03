// ============================================================
// FRAGMENT (action) — action-rename-element.cs
// PURPOSE: Rename each element in `elements` to `newName` via Element.Name (works for most nameable
//          elements — views, sheets, levels, families/types, groups, materials — not for elements that
//          don't have an independent Name, like most instance-placed geometry).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// NOT YET LIVE-VERIFIED in this session (bridge wasn't connected when this was written) — test on ONE
// element first before trusting it on a batch. Revit requires names to be unique within their own scope
// (e.g. two Levels can't share a name) — renaming more than one element to the exact same `newName` will
// succeed for the first and fail (skipped, not silently overwritten) for the rest; that's expected, not a
// bug, unless the request was actually a sequential rename (use action-renumber-sequential.cs for that).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string newName = "New Name";
// ---- END INPUTS ----

int renamed = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Rename Element"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            try
            {
                e.Name = newName;
                renamed++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Id {e.Id.IntegerValue} ('{e.Name}'): {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Renamed {renamed} element(s) to '{newName}', skipped {skipped} (name collision, or this element type doesn't support renaming).");
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to rename — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
