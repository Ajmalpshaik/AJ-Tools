# Reply Style

How to answer Ajmal, separate from what to build or fix.

- **Quantity/count questions** ("how many VCDs", "how many mechanical equipment"): answer with the bare
  number, one line. No table, no extra prose, unless Ajmal asks for sizes/breakdown.
- **Size/breakdown, when asked**: a **schedule-style markdown table** — `Size (mm) | Qty` (add more
  columns like Total Length when relevant) — one row per distinct size, **sorted by size ascending**
  (smallest to largest, e.g. 75x75, 100x100, 125x125 ... 1524x470), never by qty or by another column.
  Not an inline compact list either way — it should read like a Revit schedule, not a sentence.
- **A specific/narrowed value, not a full breakdown** ("the 300x300 VCDs", "the ones on Level 2", "the
  ones with Mark X") — this is a request for the actual **items**, not just a count or a size table. List
  each matching element with its **Family/Type (or Category) AND Element ID** — a small table
  (`Id | Family and Type | ...`) rather than a bare count. The reason: a narrowed-down request like this
  is almost always the setup for a next step ("now select those", "now move them", "what's their length")
  — the Element IDs are what make that next step possible without re-filtering from scratch. Compose
  [`filters/filter-by-category-and-numeric-param.cs`](../scripts/filters/filter-by-category-and-numeric-param.cs)
  (or whichever filter matches) with
  [`actions/action-report-parameters.cs`](../scripts/actions/action-report-parameters.cs) for this —
  `action-count-and-report.cs` is for a bare count/aggregate breakdown, not this case.

- **Substantive work** (a build/fix/check, or anything touching the live model — not a quick count/size
  question) — close with this **7-point Final Report**:
  1. What I understood the request to be
  2. What already existed (in the project, or found online) that got reused
  3. Whether this was a split / update / create, and why
  4. What was live-tested, and the real result
  5. What still needs Ajmal's decision before it's used for real work
  6. What got saved/documented so next time is faster
  7. Any good next step toward the bigger goal, flagged without being asked
  Keep it plain-language and only as long as the work warrants — a small fix can answer all 7 points in
  a few lines; don't pad it out. This is separate from (and doesn't replace) the bare-number/table rules
  above, which are for quick queries, not finished pieces of work.

Update this file directly whenever Ajmal asks for a different reply format — it's meant to change often
and stay small.

### Log
- Seed entry — quantity answers should be just the number, nothing more, by default; size/breakdown
  answers should be a schedule-style table (Size | Qty), sorted by size ascending, not an inline list and
  not sorted by quantity.
- Seed entry — a standing 7-point "Final Report" format applies to any substantive request (build/fix/
  check/live-model change), on top of (not instead of) the quick-answer rules above.
- 2026-07-28 — Restored. This file was removed in the 2026-07-22 Brain-split cleanup, but CLAUDE.md and
  two script-fragment comments still depend on it, and reply format is cross-cutting (applies to plugin-dev
  sessions here too, not just modeling). Restored from the Brain's genericized copy with the two script
  links re-pointed at this repo's flat `scripts/` layout. The Brain keeps its own copy; the two may
  diverge on purpose.
