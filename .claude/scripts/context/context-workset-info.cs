// ============================================================
// SCRIPT (context) — context-workset-info.cs
// PURPOSE: Report whether the document is workshared, and if so, list every user workset with its
//          open/closed state and owner. Read-only, zero input, safe to call anytime.
// ============================================================

var sb = new System.Text.StringBuilder();
bool shared = Document.IsWorkshared;
sb.AppendLine($"Workshared: {shared}");

if (shared)
{
    var worksets = new FilteredWorksetCollector(Document).OfKind(WorksetKind.UserWorkset).ToWorksets();
    sb.AppendLine($"User worksets ({worksets.Count}):");
    foreach (var ws in worksets)
        sb.AppendLine($"  - {ws.Name} (Id {ws.Id.IntegerValue}, {(ws.IsOpen ? "open" : "closed")}, owner: {ws.Owner})");
}
else
{
    sb.AppendLine("This document is not workshared — no user worksets exist.");
}

return sb.ToString();
