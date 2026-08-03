// ============================================================
// FRAGMENT (action) — action-delete-elements.cs
// PURPOSE: Permanently delete every element in `elements`. This is the one action fragment with no safe
//          undo built in beyond Revit's own native Undo — treat it as the highest-risk action in this
//          folder.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// MANDATORY before running this: run the filter ALONE first (see README's "explorer first" discipline),
// show the user the real count/preview, and get an explicit go-ahead — never chain this onto a filter
// unconfirmed, no matter how small the set looks. This action also requires the bridge call itself to be
// made with allowDestructive: true (the bridge refuses Delete otherwise) — that is a second, independent
// confirmation gate, not a substitute for asking the user first.

int deleted = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Delete Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            try
            {
                // Some elements (e.g. one end of a hosted pair, or an element already removed by a prior
                // deletion in this same batch cascading through dependents) can no longer be deleted by
                // the time this loop reaches them — skip, don't fail the whole batch.
                if (!Document.GetElement(e.Id).IsValidObject) { skipped++; continue; }
                Document.Delete(e.Id);
                deleted++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Id {e.Id.IntegerValue}: {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Deleted {deleted} element(s), skipped {skipped}.");
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to delete — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
