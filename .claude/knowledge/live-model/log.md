# Live Model — Log

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## Log
- 2026-07-08 — Created this file, split out of `ajtools-conventions.md` (which is about the compiled
  plugin project, not ad-hoc AutoDebugger scripts — different context, different gotchas).
- 2026-07-08 — Added the geometric MEP connectivity trace method (connector-position matching, not
  `IsConnected`) after using it successfully to trace all 4 CRAC refrigerant systems.
- 2026-07-08 — Added: verify view state (isolation, color overrides) with a read-back before assuming an
  earlier turn's result still holds — caught color overrides that had been cleared between messages.
- 2026-07-08 — Added: use Revit's native Undo (`UIApplication.PostCommand` + `PostableCommand.Undo`) to
  revert a flagged mistake, instead of a hand-written delete script; also, treat Ajmal saying he already
  undid something himself as ground truth requiring a fresh state check.
- 2026-07-08 — Added the HVAC air terminal layout recipe (Space airflow params, matched supply/return
  count, 2-row checkerboard grid, duplicate-"Flow"-parameter gotcha) backing the new
  `ajtools-hvac-terminal-layout` skill.
- 2026-07-08 — Added: checkerboard grid orientation must auto-detect each room's long side (compare X vs Y
  extent of the shrunk usable rectangle) instead of hardcoding X=columns/Y=rows — caught after a re-layout
  put columns along the short side in 3 of 7 rooms.
- 2026-07-09 — Added the trunk-slicing-for-sizing recipe: offset the cut past each takeoff's fitting body
  (half trunk width + margin), skip the cut after the last takeoff, and — critical — slice directly at the
  final offset point rather than slicing at the takeoff's center and relocating the joint afterward, which
  was confirmed to silently delete the takeoff and orphan its branch.
- 2026-07-14 — Tagged all 1812 ducts in "1 - Mech" (1092 eligible after the horizontal/≥1000mm filter).
  Found the combined tag-vs-tag/tag-vs-duct cleanup pass, despite this file's own prior log entries saying
  it was "now the standard PASS 2 in `tag-elements-in-active-view.cs`," was NOT actually present in the
  saved recipe file — only the registry-based placement (PASS A/B/C) and the own-leader elbow-correction
  pass were. Re-implemented it (straight-leader-moves-not-L-shaped, tag-vs-tag + tag-vs-duct in one
  iteration loop, 20-iteration cap) and appended it to the recipe as "PASS D." **Lesson**: a knowledge-file
  claim that something is "baked into" a script is a claim about the file's state *when it was written* —
  verify against the actual current file content before relying on it, same as any other "trust but verify"
  case in this project. Residual after cleanup: 2 tag-vs-tag / 4 tag-vs-duct (down from 7/36), confined to
  one dense pocket, same kind of geometric deadlock as the 2026-07-14 1092-tag session noted above — cleanup
  prioritizing clash-free cost 5 of 546 horizontal tags their correct flow-direction side (541/546), the
  same accepted trade-off as before.
- 2026-07-16 — Added the full MEP Color Data Standard sync recipe (Excel → Duct/Pipe System Types →
  Materials → View Filters): abbreviation-based matching (never whole-name matching), the custom
  Discipline_Code/Service_Code/System_Name/etc. parameters already present on both system type classes,
  the Material-has-no-Keywords-in-2020-API gotcha, the KED/RAD shared-material cross-reference bug, the
  View Filter folder-naming (`/`) convention, and the projection-vs-cut override gotcha (Revit never
  defaults one from the other — both must be set explicitly per filter per view).

