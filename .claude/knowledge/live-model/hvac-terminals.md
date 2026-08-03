# Live Model — HVAC air terminal layout

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## HVAC air terminal layout — Space airflow params, terminal count, checkerboard grid, Flow parameter
Full recipe backing the `ajtools-hvac-terminal-layout` skill. Every number below (rate, ton size, return
fraction, max L/s/terminal, min count, wall clearance) is an **input Ajmal gives per request** — the code
patterns are reusable, the constants are not.

**Space airflow parameters** (on `Autodesk.Revit.DB.Mechanical.Space`, found via `space.Parameters` +
`Definition.Name.ToLower().Contains("airflow")` when in doubt about a Revit version's exact naming):
- `Specified Supply Airflow` = `BuiltInParameter.ROOM_DESIGN_SUPPLY_AIRFLOW_PARAM` (also exposed as the
  strongly-typed `Space.DesignSupplyAirflow` double property) — directly settable, no mode switch needed.
- `Specified Return Airflow` = `ROOM_DESIGN_RETURN_AIRFLOW_PARAM` (`Space.DesignReturnAirflow`) — **only
  takes effect if the mode dropdown is switched first**: `space.ReturnAirflow =
  Autodesk.Revit.DB.Mechanical.ReturnAirflowType.Specified;` (the enum lives at
  `Autodesk.Revit.DB.Mechanical.ReturnAirflowType`, values: `Specified`, `SpecifiedSupplyAirflow`,
  `CalculatedSupplyAirflow`, `ActualSupplyAirflow`). Setting the double alone without this leaves the
  Specified Return Airflow field ignored by Revit.
- Unit conversions used throughout: `UnitUtils.ConvertToInternalUnits/FromInternalUnits` with
  `DisplayUnitType.DUT_SQUARE_METERS` (area), `DUT_CUBIC_FEET_PER_MINUTE` (CFM), `DUT_LITERS_PER_SECOND`
  (L/s) — this project's Revit is 2020, so `DisplayUnitType`, not the 2021+ `UnitTypeId`.
- Resizing a Room's boundary (moving walls) automatically recomputes an already-created Space's `Area` too
  — both derive live from the same room-bounding geometry, no manual Space resize needed after a room
  resize.

**Terminal count, matched supply/return**: derive count from
`Math.Max(minCount, (int)Math.Ceiling(totalLs / maxLsPerTerminal))` using **supply's** total (always ≥
return's, since return is some fraction of supply) and reuse that same count for return too — don't compute
return's count independently, or it can come out one lower and mismatch (confirmed happened: 6 supply vs. 5
return before Ajmal corrected it). Each terminal's individual flow is then just `roomTotal / count`.

**Checkerboard grid, guaranteed even split regardless of parity**: laying a room's usable rectangle out as
exactly **2 rows × N columns** (N = the matched count from above) and assigning type by `(row + col) % 2`
always yields exactly N of each type, whether N is odd or even — because with exactly 2 rows, the two rows'
parities are complementary and cancel out overall even when a single row isn't 50/50 itself. This is why 2
rows was chosen over a squarer grid; a squarer arrangement only balances cleanly when the column count
happens to be even.

**When a room has more than one FCU, split its terminals into zones by nearest-FCU, and bound each FCU's
main duct to its own zone.** Ajmal added a 2nd FCU to King Room by hand (2026-07-08) and explained the rule
himself: each terminal belongs to whichever FCU is physically closest (`terminals.GroupBy(t =>
fcusInRoom.OrderBy(f => f.Location.DistanceTo(t.Location)).First().Id)`), giving N separate zones for N
FCUs in one room. **Critically, a zoned main duct must NOT run to the room's actual far wall** the way a
single-FCU main duct does — that would cross straight through the other FCU's zone. Instead, its endpoint
is just past its OWN zone's farthest assigned terminal (same clearance value as an overshoot past the
terminal's coordinate, e.g. `farTerminal.X + sign*clearance`, rather than `bbox.Max/Min.X - sign*clearance`
computed from the room wall). Everything else (draw → split 200mm from FCU → connect FCU end → connect
split joint) is identical to the single-FCU recipe per zone.

**Correction (2026-07-08, GF-04): the FCU should NOT be treated as the main duct's endpoint when
terminals sit on BOTH sides of the FCU along the long axis.** Original approach picked one direction (via
the farthest-terminal sign) and extended the duct only that way, straight out of the FCU's connector —
this leaves terminals on the other side of the FCU with no main duct anywhere near them. It's also
physically wrong at the FCU end: the connector faces one direction (e.g. `(0,1,0)`) while the duct traveled
along a perpendicular axis (e.g. X) with no elbow — a straight `ConnectTo` doesn't validate directional
alignment, so it "succeeds" without erroring even though the geometry is nonsensical. Confirmed correct
pattern (Ajmal supplied a working reference image from a different room): the **main trunk is a continuous
duct spanning the terminal grid's full extent** (near one wall to near the other, at whatever short-axis Y
the terminal rows sit at — e.g. the middle row's Y for an odd row count), independent of the FCU's own
position. The **FCU connects to this trunk as its own branch**, exactly like a terminal branch: a stub
duct starting at the FCU connector's `Origin` and running in the connector's own facing direction until it
reaches the trunk's line, split at the usual gap (200mm) from the FCU, both the FCU-end and the split-joint
connected explicitly, then `NewTakeoffFitting` taps the stub's far end into the trunk. Bonus: when the
connector's facing direction already points straight at the trunk's line (as it did here — connector faced
+Y, trunk needed to be reached by moving in +Y), the stub is a single straight run with no elbow needed at
all; an elbow would only be needed if the connector's facing direction didn't line up with the direction
to the trunk.
- **Note on `NewTakeoffFitting`**: after tapping a branch into a duct via this method, querying that duct's
  `ConnectorManager.Connectors` can return **3 connectors** (its 2 original endpoints, still open, plus a
  3rd representing the tap point) rather than splitting it into two separate `Duct` elements — don't assume
  a takeoff always produces a 2-piece trunk the way `BreakCurve` does.

**Because supply/return terminals are checkerboard-alternated, not zoned, the physically nearest terminal
to any given point is frequently the OTHER system type, not the same one.** Ajmal flagged this explicitly
(2026-07-08) while reviewing branch-duct work: never pick "the nearest terminal" (or nearest anything,
across a checkerboard-laid-out room) by proximity alone when system type matters — always filter explicitly
by system first (e.g. family name containing `"Supply"` vs `"Return"`, or the terminal's own
`DuctSystemType`), then pick nearest *within* that filtered set. This applies to branch-duct routing,
any future MEP trace/connectivity work in these rooms, and anything else that reasons about "closest X."

**Row count is also a per-request input — not fixed at 2.** Ajmal asked for a 3-row grid in the King Room
(2026-07-08, it's a very long/thin room — 2 rows left huge gaps on the short axis). At the time, the
technique used to guarantee balance for row counts other than 2 was flattening all cells into row-major
order and assigning type by a **single continuous running index** (`globalIndex % 2`) instead of
`(row + col) % 2`. **This turned out to be a real bug, corrected below (2026-07-08, "true checkerboard"
fix) — don't use continuous-index as the default.**

**Update (2026-07-08) — "near-square" row count is now the DEFAULT, replacing the flat "always 2 unless
told otherwise" rule above.** Ajmal, after seeing the King Room 3-row fix, asked for "the best" general
approach rather than a fixed default — the near-square rule picks whichever row count makes the grid
closest to square for that room's actual proportions, which avoids the "huge gaps on the short axis" King
Room problem automatically, for every room, not just long/thin ones.

Formula, applied to the **clearance-shrunk usable rectangle** (not the raw room bounding box):
```
totalCells = count * 2                                    // supply + return combined
rows = round( sqrt( totalCells * (shortExtent / longExtent) ) )
rows = clamp(rows, 1, totalCells)                          // never 0, never more rows than cells
```
Then split `totalCells` across `rows` as evenly as possible (`total/rows` + remainder to the first rows).
**Assign type with `(row + col) % 2`, NOT the continuous-index method above** — see the correction right
below; the continuous-index approach was a bug, not an improvement.

This row-count formula is a **default, not a hard rule** — if Ajmal asks for a specific row count for a
specific room (like he did for King Room), that per-request instruction always wins over the formula for
that room.

**Correction (2026-07-08) — "true checkerboard": use `(row + col) % 2`, not continuous-index, whenever
row lengths come out uniform (no remainder).** Ajmal caught this by comparing two rooms' actual terminal
patterns: GF-07 (9 terminals/row, odd) looked correct, GF-05 (4 terminals/row, even) did not — same
formula, different result, because **continuous-index only alternates correctly between rows when the
per-row count is odd**. When it's even, `globalIndex % 2` reduces to depending only on the column index,
so every row ends up with the *identical* pattern — a supply terminal in row 0 has another supply directly
across from it in row 1, row 2, etc. Checked directly by grouping placed terminals by (row, col) and
looking at the actual type at each position — this is the reliable verification method, not distance-based
"nearest neighbor" (a same-row neighbor can be geometrically farther away than a different-type neighbor in
an adjacent row, so a naive nearest-by-distance check can miss the violation entirely, the way it did here).

`(row + col) % 2` doesn't have this flaw — it gives true adjacency alternation in **every** direction
regardless of row/column parity, and empirically balances correctly for every uniform-row-length case
actually produced by the formula above (confirmed for all 7 of this project's rooms). Continuous-index
should only be considered as a fallback for the rare case where row lengths are genuinely *uneven*
(`totalCells % rows != 0`, remainder spread across the first few rows) and `(row + col) % 2` would fail to
balance in that specific configuration — check balance before falling back, don't assume it's needed.

**Grid orientation — always auto-detect the room's long side, never hardcode X=columns/Y=rows.** Ajmal
flagged this explicitly (2026-07-08) after a 250 L/s re-layout put the N-column axis along a room's short
side in 3 of 7 rooms (e.g. a room 7113mm×9846mm had columns spread across the 7113mm side instead of the
9846mm side, cramming them too close together). Compare the shrunk usable rectangle's X-extent vs Y-extent
(`xMax-xMin` vs `yMax-yMin`, after clearance is applied) and put the **2-row axis along whichever is
shorter**, the **N-column axis along whichever is longer** — don't assume the room's plan orientation lines
up with the model's X/Y axes. This generalizes the grid math above; it doesn't change which axis "rows" or
"columns" conceptually means, only which model axis (X or Y) each one is mapped to per room.

**Placement mechanics**:
- `Document.Create.NewFamilyInstance(pt, familySymbol, level, StructuralType.NonStructural)` for a
  `OneLevelBased`-placement family (check `familySymbol.Family.FamilyPlacementType` first) — `pt.Z` is used
  directly as the physical placement height, no separate offset parameter needed.
- Read the real ceiling height per room from the `Ceiling` element's
  `BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM` rather than hardcoding a height — confirmed 2400mm in
  this project's Ground Floor, but check, don't assume, for other levels/projects.
- `Autodesk.Revit.DB.Structure.StructuralType` must be **fully qualified** — see the unit-conversion note
  above in this file; a bare `StructuralType` fails to compile in this script context.

**Terminal's own Flow parameter — duplicate-name gotcha**: iterating `FamilyInstance.Parameters` on an air
terminal instance surfaces **two** parameters both named `"Flow"` — one with `Definition` as an
`InternalDefinition` whose `BuiltInParameter` is `RBS_DUCT_FLOW_PARAM`, another that resolves to
`BuiltInParameter.INVALID`. Set the terminal's individual airflow via
`fi.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM).Set(...)` — that's the one that's actually the real,
persisted value; both entries showed the same current value in testing (likely two enumeration paths to the
same underlying storage), but `RBS_DUCT_FLOW_PARAM` is the reliable one to target directly rather than
guessing from the plain iteration order.

**Finding which room a placed terminal belongs to**: `Autodesk.Revit.DB.Architecture.Room` (fully qualify —
same "bare name doesn't resolve" issue as `StructuralType`) has `IsPointInRoom(XYZ)` — cheap to loop over a
level's small room set per terminal rather than anything more elaborate.

**`Room.IsPointInRoom(XYZ)` silently returns false for a point above/below the room's own vertical
range — it's a 3D check, not just a 2D plan check.** Caught this matching FCUs placed in the ceiling void
(2800mm, above the 2400mm ceiling) back to their room: `IsPointInRoom` on the FCU's real location returned
false for every single room even though each FCU was clearly inside its room in plan. Fix: build a test
point using the element's X/Y but **the room's own `LocationPoint.Z`** instead of the element's real Z —
`new XYZ(elementPt.X, elementPt.Y, room.Location.Point.Z)` — whenever matching an element to a room by
horizontal position alone and the element sits well above or below the room's normal occupied height.

