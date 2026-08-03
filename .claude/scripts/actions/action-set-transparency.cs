// ============================================================
// FRAGMENT (action) — action-set-transparency.cs
// PURPOSE: Set surface transparency (0-100%) on every element in `elements` in the active view — for
//          "make these see-through" requests, e.g. to see ductwork behind a wall without hiding it.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: technique adapted from an external repository's
//         OperateElementEventHandler (SetTransparency case) — a different architecture, only the
//         technique was taken, not any code.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int transparencyPercent = 50; // 0 = opaque, 100 = fully transparent
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

int clamped = Math.Max(0, Math.Min(100, transparencyPercent));
View transparencyView = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (transparencyView == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Set Transparency"))
    {
        t.Start();
        try
        {
            foreach (var e in elements)
            {
                var ogs = transparencyView.GetElementOverrides(e.Id);
                ogs.SetSurfaceTransparency(clamped);
                transparencyView.SetElementOverrides(e.Id, ogs);
            }
            t.Commit();
            sb.AppendLine($"Set {clamped}% transparency on {elements.Count} element(s) in view '{transparencyView.Name}'.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to set transparency — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
