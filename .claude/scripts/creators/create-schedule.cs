// ============================================================
// FRAGMENT (creator) — create-schedule.cs
// PURPOSE: Create one new schedule (ViewSchedule) for a category, with a chosen set of fields/columns.
//          Bare schedule only — no sorting/grouping/filtering/formatting configured; that still has to be
//          set up in Revit's Schedule Properties dialog afterward, or added later as its own script if
//          this becomes a repeated need.
// PRODUCES: elements (List<Element>) — the newly created ViewSchedule, sb (StringBuilder)
// NOT STANDALONE — see scripts/README.md for how to compose (chain into
//          actions/action-place-schedule-on-sheet.cs to put it on a sheet).
// NOT YET LIVE-VERIFIED in this session (bridge wasn't connected when this was written) — run once and
// check the result in the Project Browser before relying on it for a bulk/repeated task.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctTerminal;
string scheduleName = "New Schedule";
string[] fieldNames = { "Family and Type", "Count" }; // must match GetSchedulableFields' real names for this category
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var categoryElement = Autodesk.Revit.DB.Category.GetCategory(Document, targetCategory);
if (categoryElement == null)
{
    sb.AppendLine($"Category {targetCategory} not found in this document.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Schedule"))
    {
        t.Start();
        try
        {
            var schedule = ViewSchedule.CreateSchedule(Document, categoryElement.Id);
            schedule.Name = scheduleName;

            var schedulable = schedule.Definition.GetSchedulableFields();
            int added = 0, notFound = 0;
            foreach (var wantedName in fieldNames)
            {
                var match = schedulable.FirstOrDefault(f =>
                    f.GetName(Document).Equals(wantedName, StringComparison.OrdinalIgnoreCase));
                if (match == null) { notFound++; continue; }
                schedule.Definition.AddField(match);
                added++;
            }

            elements.Add(schedule);
            t.Commit();
            sb.AppendLine($"Created schedule '{schedule.Name}' for category {targetCategory} with {added} field(s)" +
                (notFound > 0 ? $", {notFound} requested field name(s) not found on this category (check exact naming via GetSchedulableFields)." : "."));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create schedule — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with an action fragment below (e.g. action-place-schedule-on-sheet.cs), or add return sb.ToString(); to finish ----
