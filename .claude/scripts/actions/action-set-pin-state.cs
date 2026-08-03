// ============================================================
// FRAGMENT (action) - action-set-pin-state.cs
// PURPOSE: Pin or unpin every element in `elements`. Generic live-script version of AJ Tools'
//          Pin Elements operation, but driven by any reusable filter.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE - see .claude/scripts/README.md for how to compose.
// SOURCE: AJ Tools PinElementsService TrySetPinned pattern.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first and confirm the count before
// appending this action.

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
bool pinState = true; // true = pin, false = unpin
// ---- END INPUTS ----

int updated = 0, unchanged = 0, skipped = 0;

using (var t = new Transaction(Document, pinState ? "AJ Tools - Pin Elements" : "AJ Tools - Unpin Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            if (e == null || !e.IsValidObject)
            {
                skipped++;
                continue;
            }

            bool current;
            try
            {
                current = e.Pinned;
            }
            catch
            {
                skipped++;
                continue;
            }

            if (current == pinState)
            {
                unchanged++;
                continue;
            }

            try
            {
                e.Pinned = pinState;
                updated++;
            }
            catch
            {
                skipped++;
            }
        }

        t.Commit();
        sb.AppendLine($"{(pinState ? "Pinned" : "Unpinned")} {updated} element(s), unchanged {unchanged}, skipped {skipped}.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to change pin state - rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
