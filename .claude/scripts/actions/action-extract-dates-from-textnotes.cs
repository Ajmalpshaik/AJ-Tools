// ============================================================
// FRAGMENT (action) — action-extract-dates-from-textnotes.cs
// PURPOSE: Read every TextNote placed on each sheet in `elements` (from filter-by-sheets.cs), find
//          date-like text (formats like "22-JUL-2025", "22 Jul 2025", "22/Jul/2025" — day, 3+ letter
//          month, 2-4 digit year), and report the DISTINCT dates with which sheet(s) each came from.
//          Casing differences (MAY vs May) collapse to one date; invalid calendar dates are skipped so
//          a stray 3-letter word next to two numbers doesn't get reported as a real date.
// CONSUMES: elements (List<Element>, each really a ViewSheet — from filter-by-sheets.cs)
// PRODUCES: sb (StringBuilder) — appends the report; add `return sb.ToString();` after this fragment.
// READ-ONLY — no Transaction, nothing in the model is changed.
// ============================================================

var dateRegex = new System.Text.RegularExpressions.Regex(
    @"\b(\d{1,2})[\-/\s]([A-Za-z]{3,9})[\-/\s](\d{2,4})\b",
    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

var monthMap = new Dictionary<string, int> {
    {"jan",1},{"feb",2},{"mar",3},{"apr",4},{"may",5},{"jun",6},
    {"jul",7},{"aug",8},{"sep",9},{"oct",10},{"nov",11},{"dec",12}
};

// key: normalized "DD-Mon-YYYY" uppercased -> (display text, DateTime for sort, sheet labels it appeared on)
var grouped = new Dictionary<string, Tuple<string, DateTime, List<string>>>();

foreach (var el in elements)
{
    var sheet = el as ViewSheet;
    if (sheet == null) continue;

    var notes = new FilteredElementCollector(Document, sheet.Id)
        .OfClass(typeof(TextNote))
        .Cast<TextNote>()
        .ToList();

    string sheetLabel = sheet.SheetNumber + " - " + sheet.Name;

    foreach (var note in notes)
    {
        string text = note.Text ?? "";
        foreach (System.Text.RegularExpressions.Match m in dateRegex.Matches(text))
        {
            string monStr = m.Groups[2].Value.ToLowerInvariant().Substring(0, Math.Min(3, m.Groups[2].Value.Length));
            if (!monthMap.ContainsKey(monStr)) continue; // not a real month abbreviation, skip

            int day, year;
            if (!int.TryParse(m.Groups[1].Value, out day)) continue;
            if (!int.TryParse(m.Groups[3].Value, out year)) continue;
            if (year < 100) year += 2000;

            DateTime dt;
            try { dt = new DateTime(year, monthMap[monStr], day); }
            catch { continue; } // not a real calendar date, skip

            string monthTitle = char.ToUpperInvariant(monStr[0]) + monStr.Substring(1);
            string display = day.ToString("00") + "-" + monthTitle + "-" + year;
            string key = display.ToUpperInvariant();

            if (!grouped.ContainsKey(key))
                grouped[key] = Tuple.Create(display, dt, new List<string>());
            if (!grouped[key].Item3.Contains(sheetLabel))
                grouped[key].Item3.Add(sheetLabel);
        }
    }
}

var ordered = grouped.Values.OrderBy(v => v.Item2).ToList();

sb.AppendLine($"Unique dates found: {ordered.Count}");
foreach (var v in ordered)
{
    sb.AppendLine(v.Item1 + "  <=  " + string.Join(" | ", v.Item3));
}
// ---- add `return sb.ToString();` as the final line of the composed script ----
