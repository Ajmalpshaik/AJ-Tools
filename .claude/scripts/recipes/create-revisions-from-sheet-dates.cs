// ============================================================
// RECIPE — create-revisions-from-sheet-dates.cs
// PURPOSE: Scan every sheet's TextNotes for date-like text (e.g. "22-JUL-2025"), then create one
//          project-level Revision (Manage > Revisions) per DISTINCT date found, in chronological
//          order (oldest first) so SequenceNumber lines up with the dates. Revisions are NOT
//          associated with any specific sheet or revision cloud — this only populates the project
//          Sheet Issues/Revisions table itself.
// ORDER-DEPENDENT: dates must be created oldest-to-newest in one pass so SequenceNumber stays
//          chronological — this is why it's a recipe, not a plain filter+action composition.
// READ + WRITE — wraps element creation in a Transaction with explicit RollBack on failure.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string description = "IFI - ISSUED FOR INFORMATION";
string issuedTo = "A.Rahmani";
string issuedBy = "M.Sagheer";
// ---- END INPUTS ----

var doc = Document;
var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();

var dateRegex = new System.Text.RegularExpressions.Regex(
    @"\b(\d{1,2})[\-/\s]([A-Za-z]{3,9})[\-/\s](\d{2,4})\b",
    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

var monthMap = new Dictionary<string, int> {
    {"jan",1},{"feb",2},{"mar",3},{"apr",4},{"may",5},{"jun",6},
    {"jul",7},{"aug",8},{"sep",9},{"oct",10},{"nov",11},{"dec",12}
};

var grouped = new Dictionary<string, Tuple<string, DateTime>>();

foreach (var sheet in sheets)
{
    var notes = new FilteredElementCollector(doc, sheet.Id).OfClass(typeof(TextNote)).Cast<TextNote>().ToList();
    foreach (var note in notes)
    {
        string text = note.Text ?? "";
        foreach (System.Text.RegularExpressions.Match m in dateRegex.Matches(text))
        {
            string monStr = m.Groups[2].Value.ToLowerInvariant().Substring(0, Math.Min(3, m.Groups[2].Value.Length));
            if (!monthMap.ContainsKey(monStr)) continue; // not a real month abbreviation
            int day, year;
            if (!int.TryParse(m.Groups[1].Value, out day)) continue;
            if (!int.TryParse(m.Groups[3].Value, out year)) continue;
            if (year < 100) year += 2000;
            DateTime dt;
            try { dt = new DateTime(year, monthMap[monStr], day); } catch { continue; } // not a real calendar date
            string monthTitle = char.ToUpperInvariant(monStr[0]) + monStr.Substring(1);
            string display = day.ToString("00") + "-" + monthTitle + "-" + year;
            string key = display.ToUpperInvariant();
            if (!grouped.ContainsKey(key)) grouped[key] = Tuple.Create(display, dt);
        }
    }
}

var orderedDates = grouped.Values.OrderBy(v => v.Item2).ToList();

var sb = new System.Text.StringBuilder();
sb.AppendLine("Dates found, revisions to create: " + orderedDates.Count);

using (var tx = new Transaction(doc, "Create revisions from sheet text-note dates"))
{
    tx.Start();
    try
    {
        foreach (var d in orderedDates)
        {
            var rev = Revision.Create(doc);
            rev.RevisionDate = d.Item1;
            rev.Description = description;
            rev.IssuedTo = issuedTo;
            rev.IssuedBy = issuedBy;
            sb.AppendLine($"Seq {rev.SequenceNumber}: {rev.RevisionDate} | {rev.Description} | To: {rev.IssuedTo} | By: {rev.IssuedBy}");
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine("FAILED, rolled back: " + ex.Message);
    }
}

return sb.ToString();
