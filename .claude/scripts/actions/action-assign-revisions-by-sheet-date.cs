// ============================================================
// FRAGMENT (action) — action-assign-revisions-by-sheet-date.cs
// PURPOSE: For every sheet in `elements` (from filter-by-sheets.cs), re-scan its TextNotes for
//          date-like text, and attach (via ViewSheet.SetAdditionalRevisionIds — NOT a cloud) every
//          project Revision whose own RevisionDate string matches a date found on that sheet.
//          Preserves any revisions already attached to a sheet (union, not overwrite).
// CONSUMES: elements (List<Element>, each really a ViewSheet — from filter-by-sheets.cs)
// PRODUCES: sb (StringBuilder) — appends the report; add `return sb.ToString();` after this fragment.
// WRITES the model — wrap in a Transaction with RollBack on failure per the standard convention.
// ------------------------------------------------------------
// GOTCHA (see ../knowledge/live-model/revisions.md "Revision sequence auto-purges unused revisions"): calling
// SetAdditionalRevisionIds can trigger Revit to silently purge any OTHER project revision that isn't
// attached to any sheet and has no cloud — project-wide, not scoped to the sheet being edited. Confirm
// with Ajmal before running this if the project has revisions you're not sure are attached anywhere.
// Also: never trust Revision.SequenceNumber read inside the same transaction as this call — it can be
// stale/inconsistent until a fresh read after commit. Match revisions by RevisionDate/Description, not
// Seq, and always verify with a separate post-commit read-back.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// Restrict which project Revisions are eligible to be matched/attached — leave all blank to match
// purely by date against every revision in the project (riskier: could match an unrelated old revision
// that happens to share a date string). Recommended: scope to the batch you just created.
string onlyDescription = "IFI - ISSUED FOR INFORMATION";
string onlyIssuedTo = "A.Rahmani";
string onlyIssuedBy = "M.Sagheer";
// ---- END INPUTS ----

var doc = Document;

var revisionIds = Revision.GetAllRevisionIds(doc);
var dateToRevisionId = new Dictionary<string, ElementId>();
foreach (var id in revisionIds)
{
    var rev = doc.GetElement(id) as Revision;
    if (rev == null) continue;
    bool matchesScope =
        (string.IsNullOrEmpty(onlyDescription) || rev.Description == onlyDescription) &&
        (string.IsNullOrEmpty(onlyIssuedTo) || rev.IssuedTo == onlyIssuedTo) &&
        (string.IsNullOrEmpty(onlyIssuedBy) || rev.IssuedBy == onlyIssuedBy);
    if (matchesScope)
        dateToRevisionId[rev.RevisionDate.Trim().ToUpperInvariant()] = id;
}

var dateRegex = new System.Text.RegularExpressions.Regex(
    @"\b(\d{1,2})[\-/\s]([A-Za-z]{3,9})[\-/\s](\d{2,4})\b",
    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
var monthMap = new Dictionary<string, int> {
    {"jan",1},{"feb",2},{"mar",3},{"apr",4},{"may",5},{"jun",6},
    {"jul",7},{"aug",8},{"sep",9},{"oct",10},{"nov",11},{"dec",12}
};

using (var tx = new Transaction(doc, "Assign matching revisions to sheets by text-note date"))
{
    tx.Start();
    try
    {
        foreach (var el in elements)
        {
            var sheet = el as ViewSheet;
            if (sheet == null) continue;

            var notes = new FilteredElementCollector(doc, sheet.Id).OfClass(typeof(TextNote)).Cast<TextNote>().ToList();
            var sheetDateKeys = new HashSet<string>();
            foreach (var note in notes)
            {
                string text = note.Text ?? "";
                foreach (System.Text.RegularExpressions.Match m in dateRegex.Matches(text))
                {
                    string monStr = m.Groups[2].Value.ToLowerInvariant().Substring(0, Math.Min(3, m.Groups[2].Value.Length));
                    if (!monthMap.ContainsKey(monStr)) continue;
                    int day, year;
                    if (!int.TryParse(m.Groups[1].Value, out day)) continue;
                    if (!int.TryParse(m.Groups[3].Value, out year)) continue;
                    if (year < 100) year += 2000;
                    try { var dt = new DateTime(year, monthMap[monStr], day); } catch { continue; }
                    string monthTitle = char.ToUpperInvariant(monStr[0]) + monStr.Substring(1);
                    string display = day.ToString("00") + "-" + monthTitle + "-" + year;
                    sheetDateKeys.Add(display.ToUpperInvariant());
                }
            }

            var matchedRevIds = sheetDateKeys.Where(k => dateToRevisionId.ContainsKey(k)).Select(k => dateToRevisionId[k]).ToList();
            string sheetLabel = sheet.SheetNumber + " - " + sheet.Name;

            if (matchedRevIds.Count == 0)
            {
                sb.AppendLine(sheetLabel + " -> no matching date, skipped");
                continue;
            }

            var union = new HashSet<ElementId>(sheet.GetAdditionalRevisionIds());
            foreach (var rid in matchedRevIds) union.Add(rid);
            sheet.SetAdditionalRevisionIds(union.ToList());

            sb.AppendLine(sheetLabel + " -> attached " + matchedRevIds.Count + " revision(s)");
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine("FAILED, rolled back: " + ex.Message);
    }
}
// ---- after running: re-query fresh (separate call) and match by RevisionDate, never by Seq read here ----
// ---- add `return sb.ToString();` as the final line of the composed script ----
