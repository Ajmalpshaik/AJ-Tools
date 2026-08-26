# Live Model — Family conventions (Ajmal's house standards)

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

The **standards** to follow when authoring any family: categories, reference planes, subcategories,
materials, naming and metadata. Read this before starting a new family — most of it was read off
`TRG_MECH_EQP_Fan Coil Unit_FCU_R0`, which is the reference for how his families are built.

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

### Ajmal's reference plane convention (read off `TRG_MECH_EQP_Fan Coil Unit_FCU_R0`, 2026-08-26)

His families use a deliberate three-tier scheme for `Is Reference`. Apply the same in any new family
rather than leaving Revit's default (which is Weak Reference for every API-created plane):

| Tier | What gets it | `Is Reference` |
|---|---|---|
| Named directions | The **outer physical box** of the product — the FCU's casing, a diffuser's face plate | `Left` / `Right` / `Front` / `Back` / `Bottom` / `Top` |
| Strong Reference | Anything a user must dimension, align or connect to — duct/pipe openings, spigot faces, service heights and centres, control box faces | `Strong Reference` |
| Weak Reference | Clearance zones only | `Weak Reference` |

`Defines Origin` = **Yes** on only three planes: the template's `Center (Left/Right)` and
`Center (Front/Back)`, plus the product's own **bottom** plane (`Casing Bottom` on the FCU). Everything
else is No. He also repositions `Center (Elevation)` to the real mid-height of the product
(Z=122.5 on a 245-high casing) rather than leaving it at 0.

**Integer values for `BuiltInParameter.ELEM_REFERENCE_NAME`** (confirmed by reading his own family, not
from documentation): `0` Left, `1` Center (Left/Right), `2` Right, `3` Front, `4` Center (Front/Back),
`5` Back, `6` Bottom, `7` Center (Elevation), `8` Top, `12` Not a Reference, `13` Strong Reference,
`14` Weak Reference.

**Naming**: planes are named for the thing they bound, then the direction — `Casing Left`,
`Supply Air Opening Top`, `CHW Supply Centre`, `Clearance Rear`. Not generic names.

**API note**: `NewReferencePlane(bubbleEnd, freeEnd, cutVec, view)` always creates the plane as Weak
Reference; set `ELEM_REFERENCE_NAME` explicitly afterwards in the same transaction.

### Ajmal's subcategory + material convention (read off the FCU family, 2026-08-26)

**Subcategories** are created per functional part, coloured, and given a line weight. The main body gets
weight 3; everything else 1. Line pattern is Solid (built-in id `-3000010`) except a dedicated
"Hidden Lines" subcategory which uses Dash.

| Subcategory | Colour | Weight |
|---|---|---|
| (product name, e.g. "Fan Coil Unit") | 90,92,95 dark grey | 3 |
| Electrical | 255,217,0 yellow | 1 |
| Pipework | 112,48,160 purple | 1 |
| Clearance | 255,153,0 orange | 1 |
| Hidden Lines | 0,0,0 black | 1, Dash |

**Materials** are named `<DISCIPLINE>_<TYPE>_<ABBREV>_<Material>`, derived from the family's own file
name — `TRG_**MECH**_**EQP**_Fan Coil Unit_**FCU**_R0` gives the prefix `MECH_EQP_FCU_`:

```
MECH_EQP_FCU_Galvanised Steel Sheet     MECH_ATL_SAD_Powder Coated Aluminium
MECH_EQP_FCU_Chilled Water Supply
MECH_EQP_FCU_Powder Coated Green
```
The prefix goes **first**: the Material Browser is one flat alphabetical list shared with all of Revit's
own materials, so a *suffix* scatters a family's materials through it while a *prefix* groups them in one
block, and the discipline/type segments group whole families together too. (Subcategories do not
need this — they are already nested under their category — so those keep the full family name first,
`Fan Coil Unit - Casing`.) Renaming a material is just `Element.Name`; existing parameter associations
survive it, since they are held by element id. Clearance materials get
**80% transparency**; solid materials 0%. **The subcategory colour is set to match its material's
shading colour exactly** (90,92,95 for both the FCU subcategory and its steel material; 255,153,0 for
both the Clearance subcategory and its clearance material).

**Gap found in the FCU itself (2026-08-26)**: all 5 subcategories exist and are configured, but **none
of the 8 solids is actually assigned to one** — `BuiltInParameter.FAMILY_ELEM_SUBCATEGORY` reads
`<none>` on every form, so the colours and the weight-3 setting have no effect. Materials *are* assigned
via `MATERIAL_ID_PARAM`. Creating the subcategory is only half the job; the geometry has to be pointed
at it. Check this on any family that "has subcategories but they don't do anything".

**API**: read/write subcategories through `Document.Settings.Categories` -> the family category's
`SubCategories`; `Category.LineColor`, `Category.Material`, `GetLineWeight(GraphicsStyleType.Projection)`,
`GetLinePatternId(...)`. Assign geometry with each form's `FAMILY_ELEM_SUBCATEGORY` parameter, set to the
**GraphicsStyle** element id (not the Category id).

### Reference planes get their own colour-coded subcategories (2026-08-26)

Separate from the model subcategories above, Ajmal colour-codes the **reference planes themselves** by
function, via subcategories under the annotation category `"Reference Planes"`. Every plane in the FCU is
assigned — unlike its solids, which were not. His palette (reused where a new family has the same
function):

| Reference-plane subcategory | Colour | Holds |
|---|---|---|
| Center Lines | 255,0,0 red | the origin-defining datum planes |
| Casing Outside (product's outer shell) | 0,127,0 green | the 6 planes bounding the product |
| Supply Air | 0,176,240 light blue | supply opening / spigot faces |
| Return Air | 214,0,147 magenta | return opening / spigot faces |
| Pipework | 112,48,160 purple | pipe centres and heights |
| Electrical | 255,217,0 yellow | control box faces |
| Clearance | 255,153,0 orange | clearance planes |
| Connectors | 0,0,255 blue | connector datums |

Note these repeat the model-subcategory colours for the same function (Pipework, Electrical, Clearance
are identical in both lists) — the scheme is one palette used across both.

**API notes**
- Assign with the plane's `"Subcategory"` parameter (found by name — it has no useful BuiltInParameter).
  It accepts **either** the subcategory's `Category.Id` **or** its `GraphicsStyle.Id`; the FCU stores the
  **Category id**, so use that for consistency.
- **Gotcha that cost a wasted verification pass**: a `Category` object captured *before* new subcategories
  are created does **not** see them — `rpCat.SubCategories` on the stale reference comes back without the
  new entries, so a lookup built from it is empty and every element looks unassigned. **Re-fetch the
  category from `Document.Settings.Categories` after the creating transaction commits**, then verify.
  The writes had actually succeeded; only the check was wrong. Applies to model subcategories too.
- The curve elements inside a family (`ModelLine` / `ModelArc` on style `<Sketch>`) are just extrusion
  sketch geometry — they are not reference lines and need no subcategory treatment.

**CORRECTION (2026-08-27): `FAMILY_ELEM_SUBCATEGORY` needs the CATEGORY id, not the GraphicsStyle id.**
An earlier version of this file said the opposite and it was wrong. Setting the GraphicsStyle id
*appears* to work — `Set()` returns true, and reading the id back and resolving it through
`GetElement(id) as GraphicsStyle` gives the right subcategory name — but **`AsValueString()` comes back
empty and Revit's Properties palette shows Subcategory blank**, with the element filed under "Other".
The geometry is effectively unassigned as far as the UI and any project is concerned.

**The reliable check is `parameter.AsValueString()`, not resolving the id yourself.** If it is empty, the
value is wrong however sensible the id looks. This slipped past several of my own read-backs and was only
caught when Ajmal sent a screenshot of the Properties palette — a reminder that resolving an id through
the API proves the id points at something, not that Revit accepts it in that parameter.

It cost a silent failure on 8 solids in the FCU and 3 in the diffuser. Reference planes were unaffected:
their "Subcategory" parameter genuinely does take the Category id, which is how the FCU stored them and
how I copied it.

### Subcategory NAMING — always prefix with the family name (2026-08-26)

**Subcategories belong to the category, not the family.** Two families in the same category that both
define a subcategory called "Clearance" **merge into one** in the project — turning one off turns both
off, and the Visibility/Graphics list gives no clue which family a bare "Pipework" or "Clearance"
belongs to. Ajmal's rule from 2026-08-26: every family-authored subcategory is named
**`<Family Name> - <Part>`**:

```
Mechanical Equipment            Air Terminals
  Fan Coil Unit - Casing          Supply Air Diffuser - Body
  Fan Coil Unit - Clearance       Supply Air Diffuser - Ceiling Opening
  Fan Coil Unit - Electrical      Supply Air Diffuser - Hidden Lines
  Fan Coil Unit - Pipework
  Hidden Lines   <- built-in, left alone
```
The subcategory named after the product itself becomes `<Family> - Casing` / `<Family> - Body` so the
whole set sorts together.

**A family with only ONE subcategory drops the suffix entirely** — it is just `Supply Air Diffuser`, not
`Supply Air Diffuser - Body`. Ajmal's rule, 2026-08-27: a `- Body` suffix implies sibling parts exist, so
on a single-subcategory family it is misleading noise. Suffixes are for families that genuinely split
into parts, like the FCU (Casing / Clearance / Electrical / Pipework).

**Renaming a subcategory**: `Category.Name` is **read-only** (`CS0200`). Rename the element behind it
instead — `Document.GetElement(category.Id).Name = "..."` inside a transaction. Works fine.

**Built-in subcategories cannot (and must not) be renamed.** They have **negative ids** and
`Document.GetElement(id)` returns **null** — attempting `.Name` throws a bare NullReferenceException with
no useful message. Mechanical Equipment's "Hidden Lines" is `-2009520` and is a Revit standard that
already exists in every project, so it is unambiguous and needs no prefix. **Test `GetElement(id) != null`
before renaming** to tell a user-created subcategory from a built-in one.

**Reference-plane subcategories: colour by function, LINE PATTERN by reference priority.** Colour says
what the plane is for; the pattern says how strongly it can be grabbed:

| Priority | Pattern |
|---|---|
| Named direction (Left/Right/Front/Back/Bottom/Top) — dimension-by-name, strongest | **Solid** |
| Origin datums (`Defines Origin`, the Center planes) | **Center** (dash-dot) |
| Strong Reference | **Dash** |
| Weak Reference (clearance etc.) | **Dot** |

Set with `Category.SetLinePatternId(id, GraphicsStyleType.Projection)`. Built-in Solid is `-3000010`;
"Center" `277`, "Dash" `278`, "Dot" `281` in Revit 2020.

### Material control in a family — three routes, and which to use (2026-08-26)

A solid's material can come from one of three places, in increasing order of usefulness:

| Route | Where it is set | Who can change it |
|---|---|---|
| Direct on the solid | `MATERIAL_ID_PARAM` = a material | only by editing the family |
| By Category | `MATERIAL_ID_PARAM` = invalid; the **subcategory's** material in Object Styles | only by editing the family |
| **Associated to a family parameter** | `FamilyManager.AssociateElementParameterToFamilyParameter(materialParam, familyParam)` | **the user, per type, from the Properties palette in the project** |

**Prefer the association.** Object Styles looks like central control but can only be edited inside the
family, so it is strictly worse for a family that gets loaded into projects.

**Diagnostic**: if `MATERIAL_ID_PARAM.Set(...)` throws `"The parameter is read-only"`, the material is
already **associated** to a family parameter — that is the cause, not a permissions problem. Confirm with
`FamilyManager.GetAssociatedFamilyParameter(param)`, which returns the driving family parameter or null.

**One parameter must not drive unrelated parts** — same anti-pattern as `Clearance_Side` covering both
left and right. The FCU had a single shared `Material` parameter driving the casing, the pipework AND the
control box, so galvanised casing with copper pipes was impossible. Split it per functional group,
matching the subcategories: `Casing_Material`, `Pipework_Material`, `Electrical_Material`. A clearance
solid keeps its material set **directly**, deliberately unassociated, so it never follows a product
material change.

**Re-associating**: assigning a new association over an existing one works directly; if it ever throws,
clear it first with `AssociateElementParameterToFamilyParameter(param, null)` then associate. Set the new
parameters to the CURRENT material before re-associating so nothing changes appearance, then remove the
old now-unused parameter last (`RemoveParameter`), after every solid has been repointed.

### Pipe and duct materials inside a family follow the MEP Color Data Standard (2026-08-26)

When a family contains service pipework or ductwork, colour each one from Ajmal's own standard rather
than picking a colour — `D:\Ajmal\BIM Resources\NEW\Modeling\03_Standards\MEP Color Data Standard.xlsx`
(readable with `openpyxl`; the `TrueColor (RGB)` and `Color Name` columns are the ones to use). The
colour work for System Types / Filters is covered separately in
[`mep-color-standard.md`](mep-color-standard.md) — this entry is only about the family-authoring side.

**Consequence for material parameters**: each *service* needs its own material parameter, because each
has a different standard colour. An FCU carrying chilled water supply, chilled water return and a
condensate drain needs three — `CHWS_Material` (Navy), `CHWR_Material` (Cornflower Blue),
`Drain_Material` (Dark Khaki) — not one `Pipework_Material`. Materials named per the convention above,
e.g. `MECH_EQP_FCU_Chilled Water Supply`.

**Electrical enclosures** are powder-coated green by convention — RAL 6011 reseda green,
RGB(109,134,100), as `MECH_EQP_FCU_Powder Coated Green`.

**Always verify which solid is which service by geometry, never by element order.** Compute where each
service *should* be from its own parameters (`Casing Front + CHW_Supply_Offset`, `CHW_Supply_Height`) and
match that against each solid's bounding-box centre. On the FCU this confirmed supply at Y-150/Z50,
return at Y-50/Z195, drain at Y150/Z40 — three near-identical 25mm stubs that are impossible to tell
apart any other way.

### Every family in the library carries the same metadata set (2026-08-27)

Read off the FCU and applied to the diffuser. **Add these eleven to any new family** — they are the
library-wide identity block, nothing to do with what the product is:

| Parameter | Scope | FCU | Diffuser |
|---|---|---|---|
| `Abbreviations` | Type | FCU | SAD |
| `Filter_By` | Type | FCU | SAD |
| `Family_Status` | Type | WIP | WIP |
| `Family_Source` | Type | In-House | In-House |
| `Family_Revision` | Type | R0 | R0 |
| `Family_Created_By` | Type | Ajmal PS | Ajmal PS |
| `Family_Created_Date` | Type | 2026-08-24 | 2026-08-26 |
| `Family_Approved_By` | Type | (blank until approved) | |
| `Family_Approved_Date` | Type | (blank until approved) | |
| `Model_Number` | Type | (manufacturer fills) | |
| `Serial_Number` | **Instance** | (per placed unit) | |

`Abbreviations` and `Filter_By` both take the family's own abbreviation — the same one used in the file
name and the material prefix (`MECH_EQP_**FCU**_...`). `Serial_Number` is the only Instance one, since it
identifies a physical unit rather than a type. Dates are `YYYY-MM-DD`. All live in `PG_IDENTITY_DATA`.

**Do NOT copy a family's product parameters across.** The FCU had 62 parameters the diffuser lacked, but
all except these eleven were FCU-specific — chilled water sizes, control box geometry, cooling capacity,
electrical load, clearances. Comparing two families' parameter lists is useful for finding the *shared*
metadata; everything else is meant to differ.

### A family's abbreviation must not collide with the MEP system codes (2026-08-27)

Before choosing a family abbreviation, check it against the `Abbreviation` column of
`MEP Color Data Standard.xlsx`. Those codes name **systems**, and reusing one for a **product** creates a
silent clash in schedules, filters and material names.

Caught the hard way: the square ceiling diffuser was abbreviated `SAD`, taken from its working title
"Supply Air Diffuser". But `SAD` is already **Supply Air Duct System** in the standard. The correct code
for the product is **`SCD` — Supply Ceiling Diffuser**.

The abbreviation appears in **four** places and all of them have to change together:

| Where | Was | Now |
|---|---|---|
| Family file name | `TRG_MECH_ATL_Supply Air Diffuser Square_SAD_R0.rfa` | `..._Supply Ceiling Diffuser Square_SCD_R0.rfa` |
| Material prefix | `MECH_ATL_SAD_Powder Coated Aluminium` | `MECH_ATL_SCD_...` |
| `Abbreviations` / `Filter_By` parameters | SAD | SCD |
| Model subcategory | Supply Air Diffuser | Supply Ceiling Diffuser |

Everything except the file name can be changed through the API. **The file name needs a Save As from
inside Revit** — `Document.SaveAs` is blocked from the bridge for the same reason `Save` is (an open API
transaction phase), and posting the SaveAs command just opens a dialog someone has to fill in. Ask him
to do that one step, then delete the old .rfa.

Taken system abbreviations as of this date: RAD SAD EAD FAD KED MAD PAD SED TED RWP SP VP WP DFP PFP HRP
WFP CHWR CHWS CDP REFL REFV LPG CWS HWS HWR IRWS OPD PUD SSP SCP FHP FOP CAP DLP WMP STP.
