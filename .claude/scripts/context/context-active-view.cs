// ============================================================
// SCRIPT (context) — context-active-view.cs
// PURPOSE: Quick session snapshot — the active MODEL (Revit version, document title, family vs project,
//          worksharing, all open documents) and the active VIEW (name, type, scale, associated level,
//          screen-space Right/Up directions — needed before any tag-placement or annotation script, see
//          recipes/tag-elements-in-active-view.cs), plus which views are open and how many elements are
//          selected right now. Read-only, zero input, safe to call anytime.
//          This is also the standing ping follow-up: every successful ping report includes this snapshot
//          (rule in knowledge/live-model/core.md, extended 2026-07-16).
// ============================================================

var sb = new System.Text.StringBuilder();

sb.AppendLine($"Revit {Application.VersionNumber} ({Application.VersionName})");
sb.AppendLine($"Active model: '{Document.Title}'{(Document.IsFamilyDocument ? " (FAMILY document)" : " (project document)")}");
sb.AppendLine($"Worksharing: {(Document.IsWorkshared ? "ON" : "off")}");
var openDocs = new System.Collections.Generic.List<string>();
foreach (Document d in Application.Documents) { if (!d.IsLinked) openDocs.Add(d.Title); }
sb.AppendLine($"Open documents ({openDocs.Count}): {string.Join(", ", openDocs)}");

View view = UIDocument.ActiveView;

sb.AppendLine($"Active view: '{view.Name}' (Id {view.Id.IntegerValue}, {view.ViewType})");
sb.AppendLine($"Scale: 1:{view.Scale}");

var genLevel = (view as ViewPlan)?.GenLevel;
if (genLevel != null) sb.AppendLine($"Associated level: {genLevel.Name}");

XYZ right = view.RightDirection;
XYZ up = view.UpDirection;
if (right != null) sb.AppendLine($"Screen Right direction: ({right.X:F3}, {right.Y:F3}, {right.Z:F3})");
if (up != null) sb.AppendLine($"Screen Up direction: ({up.X:F3}, {up.Y:F3}, {up.Z:F3})");

var openViews = UIDocument.GetOpenUIViews();
var openNames = openViews.Select(v => (Document.GetElement(v.ViewId) as View)?.Name ?? v.ViewId.IntegerValue.ToString());
sb.AppendLine($"Open views ({openViews.Count}): {string.Join(", ", openNames)}");

int selCount = UIDocument.Selection.GetElementIds().Count;
sb.AppendLine($"Currently selected: {selCount} element(s). (Use filters/filter-by-current-selection.cs to get the actual elements.)");

return sb.ToString();
