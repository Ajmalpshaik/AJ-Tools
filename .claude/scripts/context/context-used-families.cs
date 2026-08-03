// ============================================================
// SCRIPT (context) — context-used-families.cs
// PURPOSE: List every loadable family in the model (component families brought in from .rfa files) —
//          NOT system families like Wall/Floor/Roof types, and not in-place families. Read-only, zero
//          input, safe to call anytime.
// GOTCHA: `Family.IsEditable` is what actually distinguishes loadable/in-place families (true) from
//          system families (false) — Name/Category alone can't tell them apart.
// ============================================================

var sb = new System.Text.StringBuilder();
var families = new FilteredElementCollector(Document).OfClass(typeof(Family)).Cast<Family>()
    .Where(f => f.IsEditable && !f.IsInPlace)
    .OrderBy(f => f.Name)
    .ToList();

sb.AppendLine($"Loadable families in model: {families.Count}");
foreach (var f in families)
{
    string cat = f.FamilyCategory?.Name ?? "(no category)";
    sb.AppendLine($"  - {f.Name} (Id {f.Id.IntegerValue}, category: {cat})");
}

return sb.ToString();
