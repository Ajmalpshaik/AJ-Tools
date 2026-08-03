// ============================================================
// FRAGMENT (action) — action-set-parameter-value.cs
// PURPOSE: Bulk-set one named parameter to one value across every element in `elements` — a generic
//          version of the Flow-parameter-refresh / any other bulk parameter edit.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with Ajmal before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string parameterName = "Comments";
string stringValue = null;   // set this OR numericValueMm, not both
double? numericValueMm = null; // for Double-storage parameters, given in mm and converted internally
// ---- END INPUTS ----

int updated = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Set Parameter Value"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var p = e.LookupParameter(parameterName);
            if (p == null || p.IsReadOnly) { skipped++; continue; }

            if (numericValueMm.HasValue && p.StorageType == StorageType.Double)
            {
                p.Set(UnitUtils.ConvertToInternalUnits(numericValueMm.Value, DisplayUnitType.DUT_MILLIMETERS));
                updated++;
            }
            else if (stringValue != null && (p.StorageType == StorageType.String))
            {
                p.Set(stringValue);
                updated++;
            }
            else
            {
                skipped++;
            }
        }
        t.Commit();
        sb.AppendLine($"Set '{parameterName}' on {updated} element(s), skipped {skipped} (read-only, missing, or wrong type).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set parameter — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
