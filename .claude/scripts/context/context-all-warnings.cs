// ============================================================
// SCRIPT (context) — context-all-warnings.cs
// PURPOSE: List every warning currently in the model — severity, description, failing element Ids, and
//          the failure GUID. Read-only, safe to call anytime.
// ---- INPUTS (edit every time) ----
// errorsOnly = true narrows to Error-severity only, for a model with a long warning list.
// ============================================================

bool errorsOnly = false;   // set true to skip Warning-severity items and show only Error-severity ones
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var warnings = Document.GetWarnings();
var scoped = errorsOnly ? warnings.Where(w => w.GetSeverity() == FailureSeverity.Error).ToList() : warnings.ToList();

sb.AppendLine($"Total warnings: {warnings.Count}" + (errorsOnly ? $" ({scoped.Count} at Error severity shown)" : ""));
foreach (var w in scoped)
{
    var ids = w.GetFailingElements().Select(id => id.IntegerValue.ToString());
    Guid guid;
    try { guid = w.GetFailureDefinitionId().Guid; } catch { guid = Guid.Empty; }
    sb.AppendLine($"[{w.GetSeverity()}] {w.GetDescriptionText()} (guid {guid}) — element(s): {string.Join(", ", ids)}");
}

return sb.ToString();
