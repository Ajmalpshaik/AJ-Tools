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

### Conditional formula switch — one Yes/No toggle choosing between a formula and a typed value (2026-08-26)

A parameter that holds a formula is permanently read-only (`FamilyParameter.IsDeterminedByFormula` comes
back `true`; greyed out in Family Types), so a single parameter can never be "formula sometimes, typed
sometimes". The working pattern is three parameters, with only the last one touching geometry:

| Parameter | Type | Role |
|---|---|---|
| `Use Formula` | Yes/No | the toggle |
| `Width Manual` | Length | what the user types when the toggle is off |
| `Width` | Length | holds `if(Use Formula, Length / 2, Width Manual)` — the one that gets the `Dimension.FamilyLabel` |

Confirmed live (Revit 2020, Generic Model family) by flipping the toggle and reading `Width` back each
time: Yes -> 600mm from `Length / 2`, No -> 300mm from `Width Manual`, Yes again -> 600mm.

- **A Yes/No parameter is used bare inside `if()`** — `if(Use Formula, A, B)`, no `= 1` comparison
  needed, and (consistent with the spaced-name finding above) no quotes around the spaced name.
- **Both branches of the `if` must return the same unit type** — length on both sides. A bare number
  against a length fails with the same unhelpful generic "It is an invalid formula string."
- **Scope rule**: a formula on a *Type* parameter cannot read an *Instance* parameter; an *Instance*
  parameter's formula can read Type parameters. So if `Width` is Type, `Use Formula` and `Width Manual`
  must be Type too — otherwise make all three Instance. Mixing the wrong way is a silent design trap,
  not an obvious error.
- `fm.NewType(...)` + `fm.CurrentType` must be set **before** the first `SetFormula` (see the "no valid
  family type" entry above) — this always bites on a brand-new family, which starts with zero types.

### OmniClass for an Air Terminal family — the obvious-sounding code is the wrong one (2026-08-26)

Revit ships its own OmniClass list at
`C:\ProgramData\Autodesk\RVT <year>\UserDataCache\OmniClassTaxonomy.txt` — plain extended-ASCII,
tab separated, `number <tab> Title <tab> level <tab> BuiltInCategory-id`. **Read the code out of that
file instead of recalling one**; the 4th column tells you exactly which Revit category each entry is
allowed on, which is what settles the choice.

The trap, and a clean example of this repo's "naming describes intent, not reality" rule:

| OmniClass | Title | Maps to |
|---|---|---|
| `23.75.70.21.24` | Air Terminal **Units** | Mechanical Equipment (`-2001140`) — these are VAV boxes |
| `23.75.70.21.27.11` | Diffusers, Registers, and Grilles | **Air Terminals** (`-2008013`) |

So an Air Terminals family (diffuser/grille/register) takes `23.75.70.21.27.11`, NOT the entry whose
title literally says "Air Terminal". Picking by name alone lands on the wrong category every time.
Confirmed live: `(int)BuiltInCategory.OST_DuctTerminal == -2008013`, `OST_MechanicalEquipment == -2001140`.

**Setting it via the bridge**: `Document.OwnerFamily.get_Parameter(BuiltInParameter.OMNICLASS_CODE)
.Set("23.75.70.21.27.11")` inside a `Transaction`. Set the **category first** in the same transaction —
it changes which parameters exist (switching to Air Terminals immediately added `Max Flow` / `Min Flow`).
`OMNICLASS_DESCRIPTION` (OmniClass Title) is read-only and auto-fills from the number — **reading the
title back is the verification** that the code was accepted, since an invalid number leaves it empty.

### Duct Size in formulas — multiplies fine, can never be divided into (2026-08-26)

Shared parameter files often store neck sizes as `HVAC_DUCT_SIZE` while face sizes are plain `LENGTH`
(this is the case in Ajmal's own `Shared_Parameters.txt`). They are NOT interchangeable, and the rule is
one-directional — established by isolating one failing formula six ways:

| Formula | Result |
|---|---|
| `Face_Width (Length) = Neck_Width * 2` | **accepted** — Duct Size multiplies into a Length |
| `Neck_Area (Area) = Neck_Width * Neck_Height` | **accepted** — Duct Size squares into an Area |
| `Face_Velocity = Actual_Supply_Air_Flow / Face_Area` where `Face_Area = Face_Width * Face_Height` (both Length) | **accepted** |
| `Neck_Velocity = Actual_Supply_Air_Flow / Neck_Area` where `Neck_Area = Neck_Width * Neck_Height` | **REJECTED** |
| `Neck_Velocity = Actual_Supply_Air_Flow / (Neck_Width * Neck_Height)` | **REJECTED** |
| `Neck_Velocity = Actual_Supply_Air_Flow / Neck_Area` where `Neck_Area` holds a **plain typed value** | **accepted** |

So: **the declared parameter type is not what Revit checks — it follows the source unit types back up
the whole formula chain.** An `AREA` parameter whose value came from Duct Size is not accepted as an
area on the bottom of a division, even though the identical parameter holding a typed-in number is.
The error is always the same useless `"Formula setting failed."`, which is why isolating variants
one at a time is the only way to find the real boundary.

**There IS a laundering workaround: route the dimension through a plain `LENGTH` parameter.** Define a
Length parameter from the Duct Size one, build the area from THAT, and the division is accepted:

```
Neck_Size     (Length) = Neck_Width + 0 mm      <- or bare "Neck_Width", both accepted
Neck_Area     (Area)   = Neck_Size * Neck_Size
Neck_Velocity          = Actual_Supply_Air_Flow / Neck_Area
```
Verified live: 450 neck, 320 l/s -> area 0.2025 m2 -> **1.580 m/s**, matching the hand calculation.
`+ 0 mm` is accepted, so the laundering parameter can read exactly the same as the nominal size — there
is no need to invent a fake physical offset just to make the maths legal.

**Correction to an earlier conclusion in this file.** An earlier attempt at exactly this hop was
recorded as proof that "the taint survives a Length hop — there is no workaround". That conclusion was
WRONG. Re-probed six ways (plain literal, bare assignment from a Duct Size parameter, `+ 0 mm`,
inside an `if` ladder, and from another Length parameter) — **all six were accepted**. The difference in
the failing case was that it *rewired an existing parameter that already held a Duct-Size-derived
formula*, inside the same transaction, with an intermediate `Regenerate()` before the dependent formula
was set; the successful probes built the chain on freshly created parameters and regenerated once at the
end. Exact mechanism unconfirmed — but the practical rule is solid: **a formula rejection on a chain you
have just rewired is not proof the formula is impossible. Commit, then retry in a fresh call before
concluding anything**, and prefer building a new chain over re-pointing an old one.

**Chained formulas read stale in the same call that sets them.** After committing a transaction that
set `Face_Width = Neck_Width + 140 mm`, reading the second-level dependent `Face_Height = Face_Width`
in that same `run_csharp` call returned its OLD value (600mm instead of 440mm), and `Face_Area`
computed off that stale number too. A separate call with its own `Regenerate()` transaction returned
every value correctly. **Verify chained formulas in a fresh call, never in the one that created them** —
the same "fresh reads, not recall" rule this project applies to model state applies to Revit's own
formula propagation.

**`FamilyManager.MakeInstance(familyParameter)` / `MakeType(...)` flip an existing parameter's
instance/type scope in place, cleanly** — this is the safe answer to the problem the ReplaceParameter
entry above describes. Verified on two shared parameters at once: both flipped, both stayed shared, no
corruption, no need for the RemoveParameter + AddParameter dance (which loses the parameter's `Id` and
would orphan any dimension label). Prefer this whenever scope needs changing. Scope matters for
formulas: a **Type** parameter's formula cannot read an **Instance** parameter, so anything computed
from a per-instance value (flow, velocity) must itself be made Instance first.

**Clean way to run a throwaway formula test without leaving residue**: do it all forward inside one
transaction — set the input value, `SetFormula`, `Document.Regenerate()`, read the result, then
`SetFormula(param, null)` to clear and restore the input — and commit. Don't rely on `RollBack()` to
undo it (see the ReplaceParameter corruption entry above for why rollback isn't trusted here). Verify
the cleanup in the same call by reading `param.Formula` (expect null) and `param.IsDeterminedByFormula`
(expect false).

**Adding shared parameters to a family via the bridge**: `Application.OpenSharedParameterFile()` returns
the `DefinitionFile` for whatever `Application.SharedParametersFilename` points at; build a
name -> `ExternalDefinition` dictionary from `defFile.Groups[].Definitions`, then
`FamilyManager.AddParameter(externalDefinition, BuiltInParameterGroup, isInstance)`. Wrap each add in
its own try/catch and commit once at the end — a single throw would otherwise roll back every add in the
call (the all-or-nothing rule above). Revit shared parameter .txt files are **UTF-16 LE**, so reading one
outside Revit needs `iconv -f UTF-16LE` first; `OmniClassTaxonomy.txt` by contrast is plain ASCII.

### Number literals in formulas — always write the unit suffix (2026-08-26)

A bare number CAN be added to a length in a Revit formula, but it is silently interpreted in the
document's current length display unit, which makes the family unsafe to reuse in a project set to
different units. Tested live against a 300mm `Neck_Width`:

| Formula | Result |
|---|---|
| `Neck_Width + 50` | accepted -> 350mm (the bare `50` was read as mm because the doc is in mm) |
| `Neck_Width + 50 mm` | accepted -> 350mm |
| `Neck_Width + 2 * 75 mm` | accepted -> 450mm |

**Always write `50 mm`, never bare `50`.** The suffix form is what Ajmal's own production families use —
`TRG_MECH_EQP_Fan Coil Unit_FCU_R0.rfa` carries `CHW_Return_Height = Unit_Height - 50 mm` and
`Drain_Offset = Unit_Width - 200 mm`. Note this does NOT rescue the Duct Size division problem above;
that is a separate unit-chain restriction, not a literal-formatting one.

### Formula operators and functions — `<=` / `>=` do NOT exist (2026-08-26)

Revit's formula language accepts `<` and `>` but **rejects `<=` and `>=`** with the generic
`"It is an invalid formula string."` This is the single most misleading failure in formula authoring,
because a long nested expression fails as a whole and every other part of it looks suspect. Isolated
one clause at a time on a real auto-sizing ladder:

| Clause | Result |
|---|---|
| `if(Required_Neck_Size < 150 mm, 150 mm, 225 mm)` | accepted |
| `if(Required_Neck_Size <= 150 mm, 150 mm, 225 mm)` | **REJECTED** |
| `if(Auto_Size, 150 mm, Neck_Width_Manual)` — Yes/No toggle mixing a Length literal with a Duct Size parameter | accepted |
| four `if`s nested | accepted |

**Workaround for a genuine `<=`**: add a small tolerance to the threshold — `< 150.1 mm` instead of
`<= 150 mm`. Without it, a value landing exactly on a boundary falls through to the next rung, which in
a size ladder means jumping a whole size.

**`sqrt()` works directly on an area and returns a length** — no unitless juggling needed.
`sqrt(Face_Area)` on a 590x590 face returned exactly 590mm, and
`sqrt(Designed_Supply_Air_Flow / Design_Neck_Velocity)` returns a usable length. But
`sqrt(Face_Area / 1 m2) * 1 m` — the "make it unitless first" trick people reach for — is **rejected**;
Revit does not accept a `m2` literal.

**Auto-sizing an air terminal from airflow — the working pattern.** Target velocity must be its own
input parameter, never the calculated one, or the formula is circular:
```
Required_Neck_Size = sqrt(Designed_Supply_Air_Flow / Design_Neck_Velocity)
Neck_Width         = if(Auto_Size,
                        if(Required_Neck_Size < 150.1 mm, 150 mm,
                        if(Required_Neck_Size < 225.1 mm, 225 mm,
                        if(Required_Neck_Size < 300.1 mm, 300 mm,
                        if(Required_Neck_Size < 450.1 mm, 450 mm, 600 mm)))),
                        Neck_Width_Manual)
```
Because airflow is an **Instance** parameter, every parameter downstream of the size has to be made
Instance too (`MakeInstance`, above) — convert the **leaves first and the source last**, so a Type
parameter never momentarily reads an Instance one mid-transaction. Verified across 50-800 l/s: every
resulting neck velocity landed at or below the 2.5 m/s target (max 2.42), and the toggle flips cleanly
between the calculated size and the manual one.

**Design velocity reference**: ASHRAE puts supply diffuser neck velocity at <= 2.5 m/s for NC-30/RC-30;
CIBSE Guide B gives 2.5 m/s for a general office at NR 35-40 and 3 m/s for noise-sensitive spaces.
2.5 m/s is the defensible default — keep it as a parameter so it can be changed per project.

### Text in formulas — no number-to-text conversion, but `if()` can RETURN text (2026-08-26)

Revit 2020 has **no way to convert a number or length into text inside a formula**. Both of the obvious
attempts fail with the generic `"It is an invalid formula string."`:

| Attempt | Result |
|---|---|
| `Neck_Width + "X" + Neck_Height` | **rejected** |
| `"" + Neck_Width` | **rejected** |
| `"fixed text"` on a Text parameter | accepted |
| `if(Neck_Width < 225.1 mm, "225X225", "450X450")` | **accepted** |
| `if(Auto_Size, "AUTO", "MANUAL")` | accepted |

So a "450X450"-style size label cannot be *built* from the dimensions — but it can be *chosen* from a
fixed list with a nested `if`, which is fine whenever the sizes come from a known ladder. Same trick
covers any derived text field (status, classification, abbreviation).

**Boolean logic is function-form only**: `and(a, b)` works, the infix `a and b` is **rejected**. Same
applies to `or(...)` and `not(...)`.

**Guard against a wrong label.** A pure `<` ladder mislabels any off-list value — a hand-typed 380mm
neck would still read "450X450". Bracket each rung instead and fall through to a `"CUSTOM"` default:
```
if(and(Neck_Width > 149.9 mm, Neck_Width < 150.1 mm), "150X150",
if(and(Neck_Width > 224.9 mm, Neck_Width < 225.1 mm), "225X225",
 ... , "CUSTOM")))
```
Verified: auto-sized values label correctly across all five sizes, a manual 300 or 600 labels correctly,
and a manual 380 reports `CUSTOM` rather than silently lying.
