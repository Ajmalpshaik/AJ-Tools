# Live Model — Family geometry (extrusions, blends, connectors, symbolic lines)

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

Creating and constraining actual geometry through the bridge: sketch planes, extrusions, blends,
alignments, duct connectors and 2D symbolic lines — plus the worked builds these were learned on.

## Building a parametric family from scratch (Family Editor, via the bridge)
First done 2026-07-16: built a square ceiling air terminal (Generic Model template → category switched
to Air Terminals) with a fully parametric box body, a rectangular duct neck, and a working duct
connector — entirely via `run_csharp` against the open family document (the bridge works the same way
against a family document as a project document; `Document.IsFamilyDocument` confirms which). See
`.claude/scripts/recipes/create-parametric-box-family-with-duct-connector.cs` for the working, reusable
version of everything below.

**Switching a family's category**: `Document.OwnerFamily.FamilyCategory = Document.Settings.Categories
.get_Item(BuiltInCategory.OST_DuctTerminal)` inside a `Transaction` — `OST_DuctTerminal` is the
BuiltInCategory for the "Air Terminals" display name. Switching category immediately adds that
category's standard built-in type parameters (Max Flow, Min Flow, Cost, Description, Manufacturer, etc.)
with no extra step needed.

**`ReferencePlane`'s 3rd constructor argument is a direction vector, not a point.**
`Document.FamilyCreate.NewReferencePlane(XYZ bubbleEnd, XYZ freeEnd, XYZ cutVec, View view)` — passing
an actual coordinate (e.g. offset to match the line's X position) for `cutVec` instead of a pure unit
vector (`XYZ.BasisZ`) produces a visibly tilted plane (`Normal` comes out non-axis-aligned, e.g.
`(-0.95, 0, -0.31)` instead of `(1,0,0)`) — caught by reading `plane.GetPlane().Normal` back immediately
after creation, which is now the standard verification step for any new reference plane. Always pass a
plain unit vector (`XYZ.BasisZ` worked for both the X-normal and Y-normal planes needed here).

**Making extruded geometry actually track a reference plane requires an explicit `NewAlignment`, not just
coincident coordinates at creation time.** Sketching a profile at the same XYZ coordinates as a reference
plane does NOT bind them — changing the plane's position later (via a labeled dimension) leaves the
geometry exactly where it was. The real link: get the solid's planar side `Face` (via
`extrusion.get_Geometry(new Options{ComputeReferences=true})`, matched by `PlanarFace.FaceNormal`,
`.Reference` on the match), then `Document.FamilyCreate.NewAlignment(view, referencePlane.GetReference(),
face.Reference)` inside a `Transaction`. `view` must be a 2D view (a `ViewPlan`, e.g. the template's own
"Ref. Level" floor plan) — a 3D view doesn't work for `NewAlignment`/`NewDimension`.

**Symmetric parametric resize about a center reference plane needs an EQ dimension chain, not just a
labeled overall dimension.** A single 2-reference dimension between Left and Right planes, labeled to a
Length parameter, does resize on parameter change — but Revit's regen has no reason to keep it centered
(one plane can end up doing all the moving, drifting the whole solid off the family's own insertion
origin). The correct pattern (confirmed working, resize test showed the box staying exactly centered on
origin through repeated non-square parameter changes):
1. A 3-reference `Dimension` (Left plane, the template's own `Center (Left/Right)` plane, Right plane) —
   `dimension.AreSegmentsEqual = true` forces the two segments equal, keeping Left/Right symmetric about
   Center automatically.
2. A separate 2-reference `Dimension` (Left, Right) on a different offset dimension line, with
   `dimension.FamilyLabel = lengthParam` — this is what actually drives the overall size from the
   parameter; combined with the EQ constraint above, Revit resolves both planes moving symmetrically.
3. Repeat for the other axis using the template's `Center (Front/Back)` plane and a Width parameter.
Both `NewDimension` calls need a `Line` argument purely for where the annotation draws — offset it well
outside the geometry's own footprint (e.g. body-half-extent + 150mm/300mm) so dimension lines for
different axes/parts (body vs. neck) don't overlap and become unreadable.

**Driving extrusion depth (or any element's own double parameter) from a family parameter is simpler
than geometry dimensioning — use `FamilyManager.AssociateElementParameterToFamilyParameter`, not a
dimension at all.** `extrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM)` gives the element's
own depth parameter; `familyManager.AssociateElementParameterToFamilyParameter(thatParam, familyParam)`
inside a `Transaction` links them permanently — changing the family parameter's value regenerates the
extrusion's depth directly, no reference planes or dimensions involved. Same technique works for a duct
connector's own `CONNECTOR_WIDTH`/`CONNECTOR_HEIGHT` built-in parameters (see below) — any writable
element `Parameter` can be associated this way, it isn't specific to extrusions.

**A second solid extrusion stacked in Z doesn't need its own sketch plane positioned at the first
extrusion's top — just extend its own End value past the first one's, on the SAME base sketch plane
(Z=0).** For the neck sitting on top of the body: rather than working out where to place a new
`SketchPlane` at "the body's current top" (awkward to keep parametric, since that height itself is a
parameter), the neck extrusion sketches on the exact same Z=0 reference plane as the body, and its own
End value is bound to a formula parameter `"Neck Top" = Height + Neck Depth` (see next point) — so its
total Z-extent is 0→(Height+NeckDepth), which overlaps the body's 0→Height a little at the bottom. This
overlap is invisible (interior solid mass fully inside the already-solid body) since **multiple `Solid`
extrusion forms in one family are unioned automatically** — no explicit boolean/join step needed, unlike
adding a *Void* form (which does need an explicit cut).

**Family parameter formulas: spaced parameter names do NOT need quoting — quoting them is what breaks
the formula.** Tried `Height + "Neck Depth"` and `"Neck Depth" + Height` first (guessing at Revit's
usual spaced-name convention) — both throw `"It is an invalid formula string."` with an unhelpful
generic message. The bare, unquoted name works fine: `fm.SetFormula(param, "Height + Neck Depth")`.
Confirmed by isolating single-parameter formulas first (`"Height"` alone worked; `"Neck Depth"` alone
without quotes worked; `"\"Neck Depth\""` with quotes failed) before combining — worth doing that
isolation step again for any future formula that keeps failing with this same generic error, rather than
guessing at syntax variations blind.

**`ConnectorElement.CreateDuctConnector` exists on the static class, not `FamilyItemFactory`/
`Document.FamilyCreate` where the "New..." naming pattern would suggest.** Real signature (discovered
live via a deliberate wrong-arg compile error, not memory — see method below):
`Autodesk.Revit.DB.ConnectorElement.CreateDuctConnector(Document document, DuctSystemType systemType,
ConnectorProfileType profileType, Reference planarFace)` — `planarFace` must be a `Reference` to an
existing planar `Face` on the family's own solid geometry (e.g. the neck stub's outward-facing end); the
connector is created exactly on that face, position and orientation inherited from it (origin = face
center, direction = face normal). Must run inside a `Transaction`.

**Discovering an unknown Revit API method signature without guessing from memory — deliberately trigger
a compile error and read it, don't reflect.** `Document.FamilyCreate.NewDuctConnector()` and
`.NewConnector()` both don't exist (`CS1061`) — that ruled out the `FamilyItemFactory` guess entirely.
`ConnectorElement.CreateDuctConnector()` with zero args gave `CS1501` ("no overload... takes 0
arguments") — confirming the method exists, just needs args. Passing a bogus named argument
(`bogusName: Document`) gave `CS1739` naming the *actual* best-matching overload, but not full param
names. What actually nailed the exact signature: calling it with a plausible arg list and typed nulls
where a `Reference` was expected — the call **compiled successfully** and instead threw a runtime
`ArgumentNullException` naming the real parameter (`"planarFace"`) — compiling at all confirms the
signature is right, even though the call itself fails at runtime for being null. This is a reliable,
fast, three-step technique for verified (not memorized) API discovery via the bridge, worth reusing
whenever a method name/signature is genuinely unknown: (1) zero-arg call to confirm existence vs. typo,
(2) bogus named-arg call to get Roslyn's best-overload guess, (3) plausible-typed-args-with-null call —
compiling confirms the shape, the runtime exception's parameter name confirms the last unclear detail.

**A newly created `ConnectorElement`'s Width/Height do NOT inherit the planar face's actual size —
they default to a generic 1 foot (304.8mm) placeholder, exactly the same class of bug as `Duct.Create`
not inheriting its source connector's size (see the "Drawing a duct" section above).** Confirmed by
reading `conn.Width`/`conn.Height` right after `CreateDuctConnector` against a 200×200mm face — came
back 304.8×304.8mm. Fix: `conn.Width`/`.Height` are themselves **read-only properties** (same
`MEPCurve.Width` pattern) — set the real parameters instead, `conn.get_Parameter(BuiltInParameter
.CONNECTOR_WIDTH)` / `CONNECTOR_HEIGHT`, and for a genuinely parametric connector (not just a one-time
fix), associate those two parameters to the family's own Neck Width/Neck Height parameters via
`AssociateElementParameterToFamilyParameter` — same technique as the extrusion end above — so resizing
the neck automatically keeps the connector's port size in sync, verified by the full resize test below.

**`Document.Regenerate()` outside an open `Transaction` throws "Modification of the document is
forbidden... no open transaction" in a family document — AND, critically, an unhandled exception
anywhere later in the same `run_csharp` call rolls back every change made earlier in that call, even
changes from `Transaction`s that already called `.Commit()`.** First extrusion-creation attempt
committed its transaction successfully, then called a bare `Document.Regenerate()` afterward "just to be
safe" — that line threw, and the *entire* extrusion silently vanished (`FilteredElementCollector` found
nothing on the next call) even though the commit had already happened moments earlier within the same
script. Lesson: never add a speculative `Regenerate()`/verification line after a transaction commits
within the same call unless it's also wrapped in its own transaction (Commit() already regenerates on
its own) — and more generally, treat every `run_csharp` call as all-or-nothing: if anything after a
commit can throw, the commit's effects are not actually safe until the whole call returns successfully.

**Full worked resize test (the actual verification, not just "it compiled")**: after wiring up all of
the above, changed Length/Width/Height/Neck Width/Neck Height/Neck Depth to six different non-square
values in one transaction, regenerated, and read back both extrusions' bounding boxes plus the
connector's origin/width/height — every single value matched the new parameters exactly, box stayed
centered on origin, neck stayed centered on the box, connector tracked the neck's new top Z and new
W/H. This is the standard of proof to hold future family-authoring work to — "the API calls didn't
error" is not sufficient, a real parameter change + geometry read-back is.

### Second build (2026-07-16, electric motor Cooling Bar sub-family) — new findings

Building a nested face-based "cooling fin" sub-family (part of a larger electric-motor family with
6 nested sub-families) surfaced several NEW gotchas beyond the air terminal build above, plus one still
genuinely **unresolved** problem (the void-cut issue below) — read this whole subsection before
attempting another multi-solid family with void cuts, since the last item is a real, currently-unsolved
blocker, not just a solved gotcha.

**`FamilyManager.SetFormula` throws `"There is no valid family type."` if called before ANY family type
exists yet — even though `AddParameter` itself works fine with no type present.** Original order was
`AddParameter` ×N → `SetFormula` on one of them → `NewType` + `Set` values. The `SetFormula` call is
what threw, rolling back the whole transaction (including the 5 just-added parameters — same
all-or-nothing lesson as the `Regenerate()` gotcha above). Fix: always call `fm.NewType(...)` and
`fm.CurrentType = ft` **before** the first `SetFormula` call, even if the type's actual values get set
afterward — `SetFormula` needs a live current type to exist, `AddParameter` does not.

**Extrusion face `.Reference` is only reliably populated when the sketch plane is HORIZONTAL (Z-normal)
— a VERTICAL sketch plane (e.g. the template's own Y-normal "Center (Front/Back)" plane) gives a
reference on only ONE face out of six, silently.** Built a fin-bar profile on "Center (Front/Back)"
(normal `(0,-1,0)`) exactly the same way as every horizontal-plane extrusion before it — the solid came
out geometrically correct (right bounding box) but `get_Geometry(new Options{ComputeReferences=true})`
returned `Reference == null` for 5 of 6 `PlanarFace`s (only the one with normal `(0,0,1)` had a
reference). Confirmed this is about **sketch plane orientation, not the face-based document type**: a
second test extrusion built in the *same* document on the horizontal "Reference Plane" got all 6 face
references back correctly, and switching the `Options.View` between the floor plan, both elevations, and
the 3D view made no difference at all (ruled out view-dependence as the cause). **Fix: always sketch
extrusion profiles on a horizontal (Z-normal) plane, then choose which world axis is "length" vs
"width" vs "depth" by how you orient the RECTANGLE within that flat profile, rather than standing the
sketch plane itself up vertically** — e.g. for a fin that needs to stick radially outward once
face-hosted, draw the width×length footprint flat in X/Y and let Z (the extrude direction) be the radial
"sticking-out" depth, instead of drawing a width×height cross-section on a vertical plane and extruding
along length. This produces the same physical shape and is fully reference-safe. If a solid extrusion's
faces will ever need `NewAlignment`/`NewDimension` locking (i.e. anything beyond a one-off void cut),
build it on a horizontal sketch plane — full stop, don't risk the vertical-plane path even if it seems
more "natural" for the shape being modeled.

**UNRESOLVED as of 2026-07-16: void-form cuts (`NewExtrusion(isSolid: false, ...)`) do not appear to
actually remove material from an intersecting solid, checked five different ways, none showing any
volume change.** Built two triangular-wedge void extrusions (correct bounding boxes, confirmed
overlapping the solid fin bar's full cross-section) meant to bevel/taper both ends. Checked for the cut
taking effect via: (1) `Extrusion.get_Geometry(new Options())` on the solid directly — volume unchanged;
(2) an explicit `Document.Regenerate()` inside its own transaction before re-checking — no change; (3)
geometry query through the 3D view (`Options.View` = the ThreeD view) — no change; (4) a much larger,
unambiguously-overlapping test void (a 100mm cube fully enclosing the fin bar) — still zero volume
change, ruling out "my wedge just doesn't actually overlap" as the explanation; (5)
`SolidSolidCutUtils.AddCutBetweenSolids(doc, solidElement, voidElement)` — this is the *explicit*
solid-void cut registration API, and it exists (confirmed live, correct parameter order is
`(document, elementToBeCut, cuttingElement)`), but it **actively refused** to run on this plain Generic
Model family document: `"The element must be in a project document or in a conceptual model, pattern
based curtain panel, or adaptive component family."` — meaning this API is specifically for document
types where auto-cut do NOT already happen, which by its own wording implies a plain family like this one
*should* auto-cut without it. Also ruled out: same-transaction vs. separate-transaction creation of the
solid and void made no difference either. **Net: genuinely unresolved** — either (a) void auto-cut
really isn't happening here for API-created geometry the way it does for UI-drawn "Void Extrusion"
tool use, or (b) it IS cutting correctly at the true Revit engine/render level and every one of these
five query methods simply fails to reflect it, which would mean `get_Geometry()` is not a reliable way to
verify a void cut at all. Whichever it is, don't assume a void form has cut anything based on a
geometry/volume query the way this file's earlier "verify with a resize test" standard would normally
require — for void cuts specifically, get a human visual check (screenshot or Ajmal looking at the
family's 3D view) instead, until this is resolved one way or the other. If a future session solves this,
update this entry with the fix rather than leaving the "unresolved" framing stale.

### Connector settings worth checking on any MEP family (2026-08-26)

`ConnectorElement` carries settings that are easy to leave at their unhelpful defaults. Read them all with
`foreach (Parameter p in connector.Parameters)` — most are readonly, the useful ones are not.

- **`Allow Slope Adjustments`** defaults to **No** and must be turned **on for gravity services** —
  sanitary, waste, condensate drain — so the connected pipe can fall away from the unit. Leave it **off**
  for pressurised services (chilled water, domestic water): allowing slope there just lets pipes tilt by
  accident. On the FCU only the Sanitary "Drain,Out" connector needed it.
- Identify which connector is which by **`Domain`** (`DomainPiping` / `DomainHvac` / `DomainElectrical`),
  **`SystemClassification`** (`Sanitary`, `SupplyHydronic`, `ReturnHydronic`, `SupplyAir`…) and
  **`Origin`** — not by element order.
- `Flow Configuration` = `Preset` with `Flow` = 0 means the connector reports no flow. Fine for a
  condensate drain, wrong for anything that should size downstream pipework.

### Building the diffuser body — the stacking pattern that worked (2026-08-27)

Three stacked solids on **one fixed sketch plane at the origin**, no helper parameters needed:

| Solid | Sketch plane | Depth driven by | Held in place by |
|---|---|---|---|
| Face plate | Z=0, normal **(0,0,-1)** | `EXTRUSION_END_PARAM` -> `Face_Thickness` | 4 side alignments |
| Collar | Z=0, normal **(0,0,+1)** | `EXTRUSION_END_PARAM` -> `Collar_Depth` | 4 side alignments |
| Neck | Z=0, normal **(0,0,+1)** | top face **aligned to the `Neck Top` plane** | 4 side + 1 top alignment |

**ALWAYS build the sketch plane from a reference plane: `SketchPlane.Create(doc, referencePlane.Id)`.**
A plane made with `Plane.CreateByNormalAndOrigin(...)` produces geometry whose **Work Plane reads
"<not associated>"** — it floats free of the family's skeleton. It *looks* fine because the coordinates
happen to line up, and every resize test still passes, but the solid is not actually tied to anything.
**The Work Plane parameter (`SKETCH_PLANE_PARAM`) is read-only, so this cannot be repaired after the
fact — the geometry has to be deleted and rebuilt**, along with all its alignments. Get it right first
time.

**When the extrusion must run the "wrong" way, flip the reference plane, not the sketch plane.** The face
plate hangs *below* the ceiling line, which would mean `End = -Face_Thickness` — and a negative end
cannot be associated to a positive length parameter. The fix is `referencePlane.Flip()` (which reverses
the normal in place and keeps the plane's position and its dimensions intact), then build the sketch
plane from it. Flipping `Face Bottom` to point up let the frame extrude a **positive** `Face_Thickness`
from Z=-3 up to Z=0 — so its bottom sits **on** a named reference plane, and its work plane reads
"Reference Plane : Face Bottom".

**Stack each solid on the previous one's top plane — do NOT run one through another.** It is tempting to
sketch every solid on the same Z=0 plane and let a taller one pass through its neighbour (solid
extrusions union, so the buried part is invisible and every resize test still passes). Ajmal rejected
this, correctly: the neck should *sit on* the funnel, not run through it. Give each solid the previous
one's top as its **work plane** — frame on `Face Bottom`, funnel on `Ceiling Line`, neck on `Collar Top`
— flipping each reference plane to point up first so the depth stays positive. The result is a
continuous stack with each junction plane-locked, verified by checking that
`frame.Max.Z == funnel.Min.Z` and `funnel.Max.Z == neck.Min.Z` at every size.

**Rebuilding a solid destroys any connector hosted on its face.** The duct connector sat on the neck's
top face; deleting the neck to re-point its work plane silently took the connector with it, with no
error. After any geometry rebuild, **re-count the `ConnectorElement`s** and recreate what was lost,
including every setting on it (size associations, flow direction, flow configuration, flow
association) — none of that survives either.

**Horizontal (top/bottom) face alignments must be made in an ELEVATION view**; vertical side faces in a
plan view. Passing the wrong view type is what makes `NewAlignment` fail.

**`DuctSystemType` is in `Autodesk.Revit.DB.Mechanical`, not `Autodesk.Revit.DB`** — `CreateDuctConnector`
otherwise fails to compile with CS0234. Full call:
`ConnectorElement.CreateDuctConnector(doc, Autodesk.Revit.DB.Mechanical.DuctSystemType.SupplyAir,
ConnectorProfileType.Rectangular, planarFaceReference)`.

**Connector defaults that must be corrected every time**: size comes out as the 304.8mm placeholder
(associate `CONNECTOR_WIDTH`/`CONNECTOR_HEIGHT` to the size parameter); `Flow Direction` defaults to
**Bidirectional** (a supply diffuser takes air *In* from the duct); `Flow Configuration` defaults to
**Calculated**, which leaves `Flow` read-only — set it to **Preset** first, and only then can `Flow` be
associated to the family's airflow parameter.

### Blends (tapers/funnels) — two traps that cost a rebuild (2026-08-27)

A part that changes size over its height — a diffuser collar going from ceiling-opening size at the
bottom to neck size at the top — is a **Blend**, not an extrusion. An extrusion can only be one size.

**Trap 1: `NewBlend` fails if both profiles are coplanar.** It throws the useless
`InternalException: "Unexpected internal error: code 1."` — no clue what is wrong. Probed five ways:

| Attempt | Result |
|---|---|
| both profiles at Z=0 (plane-made sketch) | **fails** |
| both profiles at Z=0 (sketch from a reference plane) | **fails** |
| profile order swapped | **fails** |
| both profiles the same size, at Z=0 | **fails** |
| **top profile raised to Z = depth, base at Z=0** | **works** |

So build the top profile at the blend's own height even though the API signature suggests both live in
the sketch plane. Signature: `NewBlend(bool isSolid, CurveArray topProfile, CurveArray baseProfile,
SketchPlane sketchPlane)` — top first, base second.

**Trap 2: a blend cannot be constrained through its faces.** Its sides are *sloped*, so
`NewAlignment` to a vertical reference plane is impossible. Constrain the **sketch curves** instead:
after creating the blend, its two profiles exist as `ModelLine` elements on style `<Sketch>`; find them
by their coordinates (both profiles report their own sketch Z as 0, so tell them apart by size), take
`modelCurve.GeometryCurve.Reference`, and `NewAlignment(planView, referencePlane.GetReference(),
curveReference)`. All 8 alignments took first time — 4 base curves to the opening planes, 4 top curves to
the neck planes.

**Its height parameter is called "Second End"**, not anything containing "depth" or "height"
(`BLEND_END_PARAM`; `BLEND_START_PARAM` is "First End"). Associate that to the depth parameter.

**`Blend.TopProfile` / `BottomProfile` return a `CurveArrArray`, not a `CurveArray`** — iterating them
as `Curve` throws an InvalidCastException. They are only useful for reading; the constraining is done
through the ModelCurves above.

Verified across 120 / 320 / 700 l/s: bottom always equals `Ceiling_Opening_Width`, top always equals
`Neck_Size`, height always equals `Collar_Depth`.

### Stepped louvre cores — geometry pattern (2026-08-27)

A 4-way diffuser core is a stack of nested square blends, each stepping inward and downward. Built
parametrically so it scales with the diffuser:

| Piece | Spans | Work plane |
|---|---|---|
| cone 1 | `Ceiling_Opening_Width` -> `Core_Ring_1_Size` | `Core Level 2` |
| cone 2 | `Core_Ring_1_Size` -> `Core_Ring_2_Size` | `Core Level 3` |
| cone 3 | `Core_Ring_2_Size` -> `Core_Centre_Size` | `Core Level 4` |

Ring sizes are **proportions of the opening** (`Ceiling_Opening_Width * 0.78 / 0.56 / 0.34`) so the core
always rescales; the levels are reference planes spaced by `Core_Step = Core_Depth / 3`, each flipped to
point up so every blend extrudes with a positive depth. Each ring size gets its own 4 reference planes
with an EQ chain and a labelled dimension, exactly like the main body — 12 planes, 12 dimensions,
24 alignments for the three cones.

**Cone count cannot be driven by a parameter.** Revit families cannot create or destroy geometry, only
resize it. It can be *faked* by building the maximum number of cones and hiding some with visibility
formulas — but real diffusers keep the same cone count across their whole size range and simply scale,
so a fixed count is the more accurate model as well as the simpler one.

**Outcome on the diffuser: Ajmal removed it.** The three-cone core was built and worked correctly, but he
judged it ugly and had it stripped out — cones, the 15 reference planes and all 5 `Core_*` parameters.
**Do not rebuild a louvre core on `TRG_MECH_ATL_Supply Air Diffuser Square_SAD_R0` unless he asks.** The
family is deliberately a plain frame + funnel + neck. Keep the technique above for a family that wants
it; treat this one as settled.

**Removing a whole feature cleanly** — delete in dependency order inside one transaction: solids first,
then the reference planes (their dimensions are destroyed with them automatically), then
`RemoveParameter` for each parameter, which only succeeds once nothing references it. Verify afterwards
with the normal resize test that nothing else was collateral damage, and note that the shared parameter
*definitions* stay in `Shared_Parameters.txt` — the API cannot delete them, so unused entries accumulate
there.

### Symbolic lines (2D annotation geometry in a family) — API notes (2026-08-27)

`Document.FamilyCreate.NewSymbolicCurve(Curve, SketchPlane)` creates plan-only geometry that shows in
plan and ceiling views and adds nothing to the 3D model — the right tool for airflow arrows, direction
symbols and the like.

- **A `FilteredElementCollector` cannot be given `typeof(SymbolicCurve)`** — Revit rejects it with
  *"element type that exists in the API, but not in Revit's native object model"*. Collect
  `typeof(CurveElement)` and filter with `is SymbolicCurve`.
- **`FamilyElementVisibility` property names are not what they read like.** The real ones are
  `IsShownInCoarse` / `IsShownInMedium` / `IsShownInFine`, `IsShownInLeftRight`, `IsShownInFrontBack`,
  `IsShownInTopBottom`, `IsShownInPlanRCPCut`, `IsShownOnlyWhenCut`. Anything like `IsShownInPlan` or
  `IsShownInRCPPlan` does not exist and fails to compile.
- **On a symbolic curve (`ViewSpecific` visibility) only the detail-level flags are settable.** Setting a
  view-direction flag throws *"Cannot set this property in view specific visibility."* — symbolic lines
  are inherently plan geometry, so direction does not apply. Set only Coarse/Medium/Fine.

**Outcome on the diffuser: Ajmal rejected the arrows and will draw his own.** They were built (4 arrows,
3 lines each, sized off `Face_Width`) and removed again. **Do not add airflow arrows to
`TRG_MECH_ATL_Supply Ceiling Diffuser Square_SCD_R0` unless he asks** — same standing decision as the
louvre core.
