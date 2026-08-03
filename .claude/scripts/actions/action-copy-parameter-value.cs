// ============================================================
// FRAGMENT (action) — action-copy-parameter-value.cs
// PURPOSE: Copy one parameter's value into a different parameter, across every element in `elements` —
//          e.g. stamp Type Mark into Comments, or mirror one shared parameter into another. Storage-type
//          aware: only copies between matching storage types (String<-String, Double<-Double, etc.);
//          skips anything that doesn't match rather than guessing a conversion.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with Ajmal before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string sourceParameterName = "Type Mark";
string targetParameterName = "Comments";
bool overwriteExisting = true; // false = skip elements where target already has a non-empty value
// ---- END INPUTS ----

int updated = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Copy Parameter Value"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var src = e.LookupParameter(sourceParameterName);
            var dst = e.LookupParameter(targetParameterName);
            if (src == null || dst == null || !src.HasValue || dst.IsReadOnly) { skipped++; continue; }
            if (src.StorageType != dst.StorageType) { skipped++; continue; }

            if (!overwriteExisting)
            {
                bool targetHasValue = dst.StorageType == StorageType.String
                    ? !string.IsNullOrEmpty(dst.AsString())
                    : dst.HasValue && dst.AsValueString() != null;
                if (targetHasValue) { skipped++; continue; }
            }

            switch (src.StorageType)
            {
                case StorageType.String: dst.Set(src.AsString()); updated++; break;
                case StorageType.Double: dst.Set(src.AsDouble()); updated++; break;
                case StorageType.Integer: dst.Set(src.AsInteger()); updated++; break;
                case StorageType.ElementId: dst.Set(src.AsElementId()); updated++; break;
                default: skipped++; break;
            }
        }
        t.Commit();
        sb.AppendLine($"Copied '{sourceParameterName}' -> '{targetParameterName}' on {updated} element(s), skipped {skipped} (missing, read-only, type mismatch, or already had a value).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to copy parameter — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
