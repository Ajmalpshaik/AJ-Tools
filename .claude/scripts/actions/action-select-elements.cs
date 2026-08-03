// ============================================================
// FRAGMENT (action) — action-select-elements.cs
// PURPOSE: Set `elements` as the active Revit selection — so after the script finishes, Ajmal sees
//          exactly this set highlighted/selected in the Revit UI, ready for him to act on manually.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see .claude/scripts/README.md for how to compose.
// ============================================================

UIDocument.Selection.SetElementIds(elements.Select(e => e.Id).ToList());

sb.AppendLine($"Selected {elements.Count} element(s) in Revit.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
