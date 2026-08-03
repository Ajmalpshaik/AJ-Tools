# Live Model — HVAC ductwork — drawing, branching, sizing, equipment placement

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## Rotating equipment to face a target direction (e.g. "FCU duct connector toward the terminals")
Used to rotate a placed FCU so its supply-air duct connector faces the centroid of that room's air
terminals (2026-07-08).
- **Identifying the right connector on Mechanical Equipment**: `FamilyInstance.MEPModel.ConnectorManager
  .Connectors` gives every connector (piping, electrical, HVAC all mixed together) — filter to
  `Domain == Domain.DomainHvac`. This FCU family exposed **two** HVAC duct connectors, both
  `Connector.DuctSystemType == DuctSystemType.SupplyAir`: one labeled `Description == "Fresh Air"` (outside
  air intake) and one with an **empty** `Description` (the real supply-air-out connector that needs to face
  the terminals). Don't assume there's only one HVAC connector on an equipment family — check
  `Description` (and `DuctSystemType`) to pick the right one.
- **Reading a connector's current facing direction**: `connector.CoordinateSystem.BasisZ` — already in world
  coordinates (accounts for the instance's current placement/rotation), no extra transform needed.
- **Computing and applying the rotation**: project both the connector's current direction and the target
  direction onto the XY plane (zero out Z), get each angle via `Math.Atan2(dir.Y, dir.X)`, and rotate by
  `targetAngle - currentAngle` (normalize into `(-π, π]` by adding/subtracting `2π`). Apply with
  `ElementTransformUtils.RotateElement(doc, elementId, Line.CreateBound(pt, pt + XYZ.BasisZ), rotation)`
  using a vertical axis through the element's own insertion point — rotates the whole instance in place.

## Drawing a duct between two points, with or without connecting it
Used to draw a main duct from each room's FCU across the room (2026-07-08).

**Correction: "main duct to the farthest terminal" does NOT mean the duct literally ends at that
terminal's connector.** First attempt used the farthest supply terminal's own connector origin as the
duct's endpoint — Ajmal rejected this (also broke down with ties: two terminals equidistant from the FCU
give an arbitrary, meaningless pick). What he actually wants, confirmed after showing him the numbers: the
main trunk runs **straight along the room's long axis from the FCU to near the far wall** (same wall-side
as the terminal grid, using the same clearance already established for terminal wall gaps — 750mm this
session), staying **level at the FCU's own height** the whole way (not dropping to the terminals' lower
Z) and **fixed at the FCU's own coordinate on the short axis** (no sideways drift). Concretely: pick the
long/short axis the same way as the terminal checkerboard grid (compare room bounding-box X vs Y extent);
travel-direction sign = which way the terminals are from the FCU (`Math.Sign(farthest terminal coord - FCU
coord)` on the long axis only, just for direction, not as the endpoint itself); endpoint's long-axis
coordinate = `(wall bbox coordinate on that side) - sign * clearance`; endpoint's short-axis coordinate and
Z both stay equal to the FCU connector's own. The "farthest terminal" is only used to pick which direction
along the axis to travel, never as the actual endpoint.

**`Duct.Create(...)` alone draws a plain, unconnected straight duct — it does NOT join anything by
itself.** Ajmal explicitly asked for just the geometry first ("no need to connect, just draw straight
duct, we will connect this after") — a real, deliberate two-step workflow, not a shortcut. Don't
auto-chain the `ConnectTo` calls below unless asked; drawing the duct and connecting it are two separate
asks that can land in different turns.

**A new duct must be explicitly sized to match its source connector — `Duct.Create` does NOT inherit the
connector's size on its own.** Ajmal undid an entire duct-drawing pass over this: the FCU's supply
connector was 1050×330mm, but the freshly created duct came out at the duct type's own default size,
producing a visible mismatch at the FCU even though geometrically the duct started at the right point.
Fix: read `connector.Width` / `connector.Height` from the source connector and set them on the new duct.
**`MEPCurve.Width`/`.Height` (the properties on `Duct`) are read-only** — setting them directly is a
compile error ("cannot be assigned to -- it is read only"). Set the actual parameters instead:
`duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(connector.Width)` and
`RBS_CURVE_HEIGHT_PARAM` for height. Do this for any duct drawn to originate at a specific piece of
equipment — always match its size to that equipment's connector, don't rely on the duct type's default.

**Clarified same session: "don't connect yet" meant the far/terminal end specifically, not the FCU end.**
Right after drawing the unconnected duct Ajmal immediately asked "why is this not from the FCU, it needs
to connect from the FCU" — so the FCU-side connection should be made as soon as the duct is drawn (it's
the duct's actual origin, not an open question), while the far end toward the terminals stays open
deliberately, since that's the "connect each terminal to the main duct" step still to come. When in doubt
about which end of a "draw first, connect later" duct to leave open, it's the end furthest from the known
source equipment, not the equipment end itself.

**Splitting an existing duct into two segments at a given point**:
`Autodesk.Revit.DB.Mechanical.MechanicalUtils.BreakCurve(document, ductElementId, pointOnCurve)` — splits
one `Duct` into two. Compute the break point by taking the FCU-side endpoint, the unit direction toward the
far endpoint, and offsetting by the gap distance: `fcuEnd + direction.Normalize() * gapFt`. Same pattern
should work for pipes via `Autodesk.Revit.DB.Plumbing.PlumbingUtils.BreakCurve` if that's ever needed.

**Correction — `BreakCurve` does NOT auto-connect the two resulting segments.** Originally assumed it did
("no coupling fitting needed, same size/system continues through") — wrong, confirmed by directly counting
connector `IsConnected` state after a split: 0 of 28 connectors (14 ducts × 2) were connected. This is what
was actually causing the recurring "duct/pipe has been modified to be in the opposite direction, causing
the connections to be invalid" error, not the FCU-connect step itself as first suspected — connecting one
end (e.g. to the FCU) while the split joint right next to it was silently unconnected left the system in an
inconsistent state that blew up on a later regenerate. **Always explicitly connect the split joint** right
after breaking: get both segments' connectors, find the one on each segment nearest the break point
(`OrderBy(c => c.Origin.DistanceTo(breakPoint)).First()`, or nearest to the other segment's known connector
origin), and call `.ConnectTo()` between them — the same pattern as connecting to any other equipment.
Verify by counting `IsConnected` across all the room's duct connectors before assuming a multi-segment duct
run is actually a single connected system.

## Branch duct from a terminal to a main duct — vertical riser + horizontal run + real fittings
Built for connecting each room's air terminals to its main duct (2026-07-08). Ajmal's routing rule: go
straight up from the terminal to the main duct's height first (vertical), THEN run horizontal over to tap
into the main duct — never a single diagonal duct straight from terminal to tap point.

- **`Connector.ConnectTo()` alone does NOT insert fitting geometry — it only makes a logical connection.**
  Using `.ConnectTo()` at the vertical-to-horizontal junction left `IsConnected == true` on both sides but
  produced **no elbow fitting at all** — Ajmal caught this immediately ("for the elbow it's not coming").
  The physical fitting has to be created explicitly with its own dedicated method: use
  `Document.Create.NewElbowFitting(connector1, connector2)` for a 90° turn between two duct connectors —
  this both creates the elbow AND makes the connection, so don't call `.ConnectTo()` as well. Same family of
  methods: `NewTeeFitting`, `NewTransitionFitting`, `NewTakeoffFitting` (used for tapping into the main
  duct — that one already produced a real tee, since it's a dedicated creation call, unlike the plain
  connect used for the elbow joint).
  **Live-verified clarification (2026-07-17): calling `.ConnectTo()` first and THEN `NewElbowFitting()` on
  the same connector pair does not error or corrupt anything** — tested directly (two test ducts meeting at
  a corner, both calls made, committed, inspected, cleaned up): `NewElbowFitting` still trims back both
  duct ends and inserts a correctly-positioned, correctly-connected elbow regardless of the prior
  `ConnectTo`. So the rule above is "don't bother calling `ConnectTo` first, it's redundant" — not "calling
  both breaks the geometry." Found while reviewing an external script (`.agents/skills/auto_route_terminals`,
  outside this project's own `.claude` setup) that called both; that script had other real problems (blind
  deletion of "diagonal" ducts, no Supply/Return filtering, missing duct sizing — see
  `ajtools-conventions-log.md` 2026-07-17 for the full review) but this particular pattern wasn't one of them.
- **Edge case: terminal already lines up under the main duct's line (near-zero horizontal offset).**
  Creating a horizontal segment of ~0 length throws "The points of startPoint and endPoint are too close:
  for MEPCurve, the minimum length is 1/10 inch." When the projected tap point is within a few mm of the
  vertical duct's top connector, skip the horizontal segment and elbow entirely — call
  `Document.Create.NewTakeoffFitting(verticalDuctTopConnector, mainDuctSegment)` directly on the vertical
  duct's own top connector.
- **Bug: re-querying "the main duct" mid-loop by category + room-containment alone also matches previously
  created branch ducts**, once a room has more than one branch already placed — `OrderBy(...Distance...)
  .First()` can then pick a branch segment instead of the actual trunk, causing bogus near-zero-length
  duct errors for later terminals in the same room. Fix: identify true main-duct segments by a precise
  geometric signature instead of just category+room — both curve endpoints must sit on the FCU's own fixed
  perpendicular-axis coordinate AND at the main duct's Z height (small tolerance, e.g. 20mm), which no
  branch duct (vertical or the short horizontal tap run) ever satisfies on both ends simultaneously.

**Capping an open duct end**: `Autodesk.Revit.DB.Plumbing.PlumbingUtils.PlaceCapOnOpenEnds(doc, elemId,
capTypeId)` **only accepts pipe curves/fittings/accessories** — passing a `Duct` id throws "The element
elemId is neither an object of pipe curve, pipe fitting, nor pipe accessory." There is **no**
`MechanicalUtils.PlaceCapOnOpenEnds` equivalent for ducts in this Revit version. Also,
`Document.Create.NewFamilyInstance(Connector, FamilySymbol)` — a hoped-for connector-based placement
overload — doesn't exist here either ("no overload takes 2 arguments").

**Correction (2026-07-08): `IsConnected == true` after a plain place-then-`ConnectTo` does NOT mean the cap
is actually correctly sized, positioned, or facing the right way.** Original approach (place a stock-size
cap instance at the open connector's origin, call `ConnectTo`) reported success and `IsConnected == true`,
but Ajmal caught it as visibly wrong twice — first the size didn't match the duct (see the "Duct Width"/
"Duct Height" instance-parameter fix below, which is still correct but not sufficient alone), then even
after fixing size, undid the whole batch and supplied his own working pyRevit tool as the reference to
study. **`ConnectTo` only makes the logical MEP-system link — it does not verify or fix the cap's actual
geometric position/orientation.** The reliable recipe (translated from that working pyRevit script):
1. **Get the cap family/type from the duct type's own Routing Preferences**, not a hardcoded family name:
   `ductType.RoutingPreferenceManager.GetRule(RoutingPreferenceRuleGroupType.Caps, i).MEPPartId` → look up
   that `ElementId` as a `FamilySymbol`. (Confirmed in this project: resolves to the same
   `M_Rectangular Endcap : Standard` already in use, so this is a consistency check as much as a discovery
   step.)
2. **Duplicate a precisely-sized TYPE** rather than relying only on instance-parameter overrides — name it
   something traceable like `AJ_AUTO_CAP_{width}x{height}mm`, check for an existing type with that exact
   name first (Ajmal's own manual work in one room had already created one; reuse it rather than
   duplicating again) and set its Width/Height parameters by searching all its `Parameters` for names like
   `"WIDTH"`, `"DUCT WIDTH"`, `"NOMINAL WIDTH"`, `"W"` (case-insensitive, skip `IsReadOnly`/non-`Double`).
3. **Place with the 3-argument `NewFamilyInstance(XYZ, FamilySymbol, StructuralType)` overload** (no
   `Level` argument) — this DOES exist and is what the working script uses; the 4-arg `Level` overload used
   earlier in this project also works, but match the point-based placement pattern that's actually verified.
4. **Set the instance's own Width/Height parameters too** (same name-search as step 2, redundant safety),
   then **directly set the cap's own connector's `Width`/`Height` (or `Radius` for round)** —
   `capConnector.Width = ductConnector.Width` — this is the step that was missing before; an instance
   parameter by itself may not actually drive the connector's real geometry for every family.
5. **Re-fetch the cap's connector after every transform** (it can become a stale reference once the
   element moves/rotates/resizes) via nearest-by-origin-distance to the target duct connector, then
   **explicitly move** the cap so the connector's `Origin` exactly coincides with the duct's open
   connector's `Origin` (`ElementTransformUtils.MoveElement` by the difference vector) — resizing can shift
   a connector's position relative to the family's insertion point, so this has to happen after sizing, not
   just once at placement.
6. **Explicitly rotate the cap to face the correct mating direction** — a cap's connector must point
   *opposite* to the duct's open connector direction, not just be logically linked to it. Compute
   `angle = capConnector.CoordinateSystem.BasisZ.AngleTo(-ductConnector.CoordinateSystem.BasisZ)`; if
   `angle > ~0.0001` rad, rotate about the cross-product axis (`capDir.CrossProduct(targetDir)`, falling
   back to crossing with `XYZ.BasisX` then `XYZ.BasisY` if that cross product is ~zero) using
   `ElementTransformUtils.RotateElement(doc, capId, Line.CreateBound(ductConn.Origin, ductConn.Origin +
   axis), angle)`. Confirmed necessary in practice — rolling this out across 6 open ends in one project,
   3 needed a real rotation (90°, 90°, 180°), only 2 needed none.
7. **Move again after rotating** (rotation can shift the connector's position slightly), THEN finally call
   `capConnector.ConnectTo(ductConnector)`.

This same manual place-then-fix-then-connect pattern is the general fallback whenever a dedicated
`Place...` utility doesn't cover the MEP domain/category you're working with — but don't stop at "it
connected without erroring"; verify size, position, and facing direction explicitly, since none of those
are validated by a bare `ConnectTo` call.
- **`Duct.Create` in this Revit version (2020) only has the XYZ-point overload** —
  `Duct.Create(doc, systemTypeId, ductTypeId, levelId, startPoint, endPoint)`. The connector-based overload
  (passing two `Connector` objects directly) does **not** exist here and fails to compile
  ("cannot convert from Connector to XYZ") — always create with `connector.Origin` for both points instead.
- **Getting the system type / duct type element IDs**: `MechanicalSystemType.SystemClassification` is an
  `MEPSystemClassification` enum (not `DuctSystemType` — those are different enums, mixing them up is a
  compile error), e.g. filter `FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType))` by
  `SystemClassification == MEPSystemClassification.SupplyAir`. Duct types: filter
  `OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctType))` by `FamilyName`/`Name` (e.g. `"Rectangular Duct"`
  / `"Radius Elbows / Taps"`).
- **Actually joining the new duct to existing equipment/terminal connectors**: after `Duct.Create`, the new
  duct has its own two end connectors via `duct.ConnectorManager.Connectors` — match each to the nearer
  target connector by `Origin.DistanceTo(...)`, then call `targetConnector.ConnectTo(ductEndConnector)` on
  **both** ends. This is what actually creates the physical join (and lets Revit insert a transition
  fitting automatically if the two connector sizes/shapes differ) — just creating the duct at the right
  points without this `ConnectTo` step leaves it geometrically coincident but not connected.
- Picking "the farthest terminal" (or any distance-based target) is a plain LINQ
  `OrderByDescending(fi => locA.DistanceTo(locB)).First()` — no special API needed.

## Placing equipment relative to a door (e.g. "FCU near the door side")
Used to move a room-center-placed FCU to sit near its door instead (2026-07-08).
- **Which door belongs to which room**: doors don't have a direct "room" property — use the **phase-based**
  `FamilyInstance.get_ToRoom(phase)` / `get_FromRoom(phase)` (get a `Phase` via
  `Document.Phases.get_Item(Document.Phases.Size - 1)` for the current/last phase), and match either side
  against the room's `Id`. A room can have more than one door; this project's rooms each had exactly one.
- **Getting the wall's in-plan direction and an inward-pointing normal**: `door.Host as Wall`, then
  `(wall.Location as LocationCurve).Curve.GetEndPoint(0/1)` to get the wall direction vector, and
  `new XYZ(-direction.Y, direction.X, 0)` for a perpendicular normal — but this doesn't tell you which of
  the two perpendicular directions points *into* the room vs. into the neighboring space. **Test it**:
  offset a small distance (e.g. 200mm) from the door's location point along the candidate normal, then
  check `room.IsPointInRoom(testPoint)` — if false, flip the normal. Don't assume a fixed sign convention,
  it depends on which way the wall's location curve happens to run.
- Final placement = door's `LocationPoint` + the confirmed inward normal × the desired inset distance,
  at whatever Z height the equipment needs (independent of this XY calculation).
- **Correction (2026-07-08): "move toward the door" means shift in ONE axis only (perpendicular to the
  door's wall), not snap to the door's exact position on both axes.** Ajmal explicitly rejected using the
  door's full location (which also pulls the along-wall/tangential coordinate to match the door) — the
  along-wall coordinate should stay wherever it already was (e.g. the room's center), only the
  perpendicular-to-wall coordinate should move toward the wall. Decompose using the wall's tangent vector
  `t` (its direction vector) and the inward normal `n`: keep the original point's component along `t`
  unchanged, replace only the component along `n`. Formula used: `finalXY = doorPt + inward*insetFt +
  t * Dot(originalPoint - doorPt, t)` — i.e. take the perpendicular offset from the door/wall, but the
  tangential (along-wall) position from wherever the equipment already was, not from the door.

## Slicing a main trunk into segments for duct sizing (progressively smaller after each takeoff)
Purpose: since each branch takeoff removes some of the trunk's airflow, the segment *after* a takeoff only
needs to carry what's left — so the trunk must be cut into separate `Duct` elements at each takeoff point,
each individually sizeable, rather than staying one uniform-size run (2026-07-09).

**The takeoff connector's `Origin` is the CENTER of the takeoff fitting's own body, not a zero-width
point.** Slicing exactly there cuts through the fitting's physical geometry. Ajmal's correction: offset the
cut downstream (away from the FCU) from the takeoff's center by `(trunk width / 2) + a clearance margin`
(started at 100mm, changed to **50mm** per Ajmal's later instruction — this margin is a per-request number,
confirm fresh each time, same convention as every other HVAC number). Also: **slice after every takeoff
except the LAST one before the end cap** — the final segment runs through the last takeoff in-line, still
one piece, all the way to the cap; don't create one more cut just for that last short stretch.

**CRITICAL BUG, confirmed by real damage: do NOT slice at the takeoff's center and then "move" the joint
afterward by editing `LocationCurve.Curve` on the two pieces.** This was the first approach tried — break
at the exact takeoff point, reconnect the joint, then stretch one piece's curve and shrink the other's to
relocate the boundary to the offset position. It silently **deleted the takeoff fitting and orphaned its
entire branch** (terminal → riser → elbow → horizontal branch, dead-ending with an open connector where the
takeoff used to be) — Revit's `IsConnected` on the terminal's own connector still showed `true` throughout,
because that only reflects the *local* terminal-to-riser link, not whether the branch actually reaches the
trunk; the break only became visible by tracing the full chain connector-by-connector until hitting an open
end. Root cause: a takeoff fitting is hosted based on being at a specific point on whichever duct element
it was created against — stretching a *different* piece's curve to cover that location doesn't transfer the
host relationship; shrinking the *original* piece away from that location leaves the takeoff's host
reference pointing at geometry that's no longer there, and Revit resolves that by dropping the fitting.

**The correct approach**: compute the desired offset break point FIRST (`takeoffCenter + (halfWidth +
margin) * downstreamDirection`), then call `MechanicalUtils.BreakCurve` **directly at that offset point**
on the still-whole (unsliced) trunk — never break at the takeoff's own center and relocate afterward. Since
the offset point is still a valid point on the original curve (as long as it's within bounds), the
takeoff — whose host reference was established back when `NewTakeoffFitting` first ran, well before any of
this slicing — ends up correctly inside whichever of the two new pieces naturally contains its true
location, with no need to move anything. Reconnect the new joint the same way as any other split (find
each piece's open connector nearest the break point, `ConnectTo` between them).

**Checkerboard layouts put two takeoffs at the exact same longitudinal position — group by position before
cutting, don't cut per-takeoff.** Live-verified (2026-07-17) on a room with 2 terminals per row (one on each
side of the trunk): both rows' takeoffs land at the identical Y (or whichever axis the trunk runs along),
tapping in from opposite lateral directions. Slicing per-takeoff would try to cut the same point twice.
Group takeoff connectors by their position along the trunk's own axis first (round to a small tolerance),
treat each distinct group as ONE cut point, and — same "skip the last one" rule as before — don't cut after
the group closest to the end cap. Re-locate the correct current trunk piece for each successive cut
geometrically (same X/Z line, break point's coordinate strictly between that piece's two endpoints) rather
than trusting a piece's element Id across cuts, since `BreakCurve` reassigns which Id keeps which segment.
Verified end-to-end after 3 cuts on a 4-row/8-terminal room: all 8 terminals still traced to the FCU via
full BFS, nothing orphaned.

**Recovering an orphaned branch if this has already happened**: trace the chain from the terminal
connector-by-connector (riser → elbow → horizontal segment) until hitting an open connector — don't trust
`IsConnected` on the terminal alone. Find the current trunk piece whose curve range geometrically contains
that open connector's location (`Curve.Distance(openConn.Origin)`, pick the nearest/smallest), and call
`NewTakeoffFitting(openConn, thatPiece)` again to re-tap it in.

## Connecting a new FCU to an already-existing open main-duct end (not drawing main duct fresh)
Different from the normal flow above: the main duct + all branches already existed (built by a past
session), only the FCU was outstanding. Placed the FCU first (Ajmal, manually, in Revit), then connected
its supply connector straight into the pre-existing open trunk end with a plain `ConnectTo` (no new duct
segment needed since the FCU was positioned right at the open connector already) — same as connecting to
any other open connector, just confirm sizes match first.

**`Duct.LevelId` can silently go invalid (`-1`) on a trunk piece after it's been through a `BreakCurve` or
`NewTakeoffFitting` split, even though the element itself is still perfectly valid and physically
connected.** Don't feed a duct's own `LevelId` into a subsequent `Duct.Create` call for a new branch off
of it without checking — use the *terminal's* `LevelId` instead (confirmed reliable), or check
`!= ElementId.InvalidElementId` first and fall back.

**A naive single-hop connector trace (`AllRefs.First()`) gives a false "broken" result at a tee/takeoff
junction** — a trunk duct with a takeoff has 3 relevant connectors (upstream, downstream, branch), and
blindly taking the first one found in `AllRefs` can walk toward a dead end (e.g. the end cap) instead of
toward the FCU, reporting "broken" on a branch that's actually fully connected. Always use a proper BFS
that enqueues *every* connector in `AllRefs` (not just the first) when verifying a branch reaches its
FCU/equipment — this is what `verify-duct-connectivity.cs` already does; don't write a shortcut linear
walk for a one-off check, it will lie at any junction.

