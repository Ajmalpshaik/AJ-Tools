# Live Model — Building parametric families (Family Editor)

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

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

### Third build (2026-07-19, bifurcation duct fitting) — ReplaceParameter/RenameParameter rollback corruption

**`FamilyManager.ReplaceParameter`/`RenameParameter`, used together to change an existing parameter's
group while keeping its name, produced real data corruption from a transaction that never committed —
the "uncommitted `Transaction` rolls back cleanly" assumption relied on elsewhere in this file does NOT
reliably hold for this specific API sequence.** Calling `ReplaceParameter(currentParameter, sameName,
newGroup, isInstance)` directly always throws `"Cannot replace a family parameter with another family
parameter, use RenameParameter() instead."` whenever `parameterName` matches the parameter's own current
name (Revit treats it as colliding with itself). The two-step fix implied by that message —
`RenameParameter(p, name+"__tmp")` then `ReplaceParameter(p, name, newGroup, isInstance)` — was tried
inside one transaction, looped over ~10 parameters. It threw the same error again partway through the
loop (plausibly a same-transaction stale-read issue, the same family of bug as this file's own
`Regenerate()`-outside-transaction lesson above), and the `Transaction` was never `Commit()`ed — it
should have rolled back everything. **It did not.** Live re-query after the exception showed: one
parameter split into two garbage duplicates with wrong values (a "Branch Spacing" parameter meant to
hold 550mm came back as two separately-named parameters holding 1120mm and 0mm), a completely different
parameter silently deleted outright (`get_Parameter` returned null, no trace), and — found later by
re-checking specifically because the above raised suspicion — two more parameters' VALUES quietly
changed (a width parameter and the main body's own driving Length parameter), even though nothing in the
failed script ever called `fm.Set()` on them. Separately, 3 `Extrusion` elements turned up in the
document where exactly 1 had ever been created — no script in the session explicitly duplicated one.

**Net effect: treat any `ReplaceParameter`/`RenameParameter` sequence that throws as leaving the entire
document's parameter *and geometry* state untrusted, not just "that one call failed."** After a failure
here, don't assume "the transaction didn't commit, so nothing changed" — re-query every parameter's name,
group, AND value (not just existence), and re-count geometry elements, before continuing. Same standard
of proof this file already requires for resize tests, just triggered by a failure instead of a success.

**Working, corruption-free alternative — but only for parameters with NO existing geometry
association** (no `Dimension.FamilyLabel`, no `AssociateElementParameterToFamilyParameter` link):
`FamilyManager.RemoveParameter(param)` followed immediately by a fresh `FamilyManager.AddParameter(sameName,
newGroup, sameType, sameIsInstance)` + `fm.Set()` to restore the value, all in one transaction. Worked
cleanly across 7 parameters, no corruption. **NOT attempted, and NOT safe, for a parameter that already
drives geometry** — removing and re-adding creates a new parameter with a new `Id`, which would orphan
any dimension label or association built in earlier steps. No verified-safe technique for changing an
already-geometry-linked parameter's group was found this session. If this comes up again: test on a
throwaway/duplicate family first, and re-verify the geometry's bounding box afterward before trusting it.
